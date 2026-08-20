using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using ScoopSearch.Indexer.Extensions;

namespace ScoopSearch.Indexer.GitHub;

internal class GitHubClient : IGitHubClient
{
    private const string GitHubApiBaseUri = "https://api.github.com/";
    private const int ResultsPerPage = 100;

    // The GitHub Search API never returns more than 1000 results for a single query, even though
    // total_count reflects the real total. Queries that exceed this are split by creation date.
    private const int MaxSearchResults = 1000;

    // The Search API allows only 30 requests per minute and additionally enforces a secondary
    // (abuse) rate limit that triggers on bursts of requests. We pace search requests just under
    // the documented limit so long paginated fetches never trip either limit.
    private static readonly TimeSpan MinSearchInterval = TimeSpan.FromSeconds(2.1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubClient> _logger;
    private readonly SemaphoreSlim _searchThrottle = new(1, 1);
    private DateTimeOffset _nextSearchRequestAt = DateTimeOffset.MinValue;

    public GitHubClient(IHttpClientFactory httpClientFactory, ILogger<GitHubClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<GitHubRepo?> GetRepositoryAsync(Uri uri, CancellationToken cancellationToken)
    {
        var targetUri = await GetTargetRepositoryAsync(uri, cancellationToken);
        if (targetUri == null)
        {
            _logger.LogWarning("{Uri} doesn't appear to be valid (non success status code)", uri);
            return null;
        }

        if (targetUri != uri)
        {
            _logger.LogInformation("{Uri} is redirected to {TargetUri}", uri, targetUri);
        }

        var getRepoUri = BuildUri("repos" + targetUri.PathAndQuery);
        var response = await _httpClientFactory.CreateGitHubClient().GetAsync(getRepoUri, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<GitHubRepo>(cancellationToken);
        }

        _logger.LogDebug("{Uri} failed with {StatusCode}", getRepoUri, response.StatusCode);
        return null;
    }

    private async Task<Uri?> GetTargetRepositoryAsync(Uri uri, CancellationToken cancellationToken)
    {
        // Validate uri (existing repository, follow redirections...)
        using var request = new HttpRequestMessage(HttpMethod.Head, uri);
        using var response = await _httpClientFactory.CreateGitHubClient().SendAsync(request, cancellationToken);

        if (request.RequestUri != null)
        {
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return request.RequestUri;
        }

        return null;
    }

    public IAsyncEnumerable<GitHubRepo> SearchRepositoriesAsync(string[] query, CancellationToken cancellationToken)
    {
        // The query is issued as-is first (no date filter). Only if it matches more than the
        // MaxSearchResults the Search API is willing to return do we split it by creation date, because
        // the repositories endpoint cannot sort by creation date and therefore cannot page past the cap
        // with a cursor. Issuing the query unmodified keeps small queries cheap and preserves the API's
        // validation errors for invalid queries (which would otherwise become a valid "match everything"
        // query once a "created:" range is appended).
        return SearchRepositoriesByDateRangeAsync(query, null, null, cancellationToken);
    }

    private async IAsyncEnumerable<GitHubRepo> SearchRepositoriesByDateRangeAsync(string[] query, DateOnly? from, DateOnly? to, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rangeQuery = from is { } fromDate && to is { } toDate
            ? query.Append($"created:{fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}").ToArray()
            : query;

        // A single day is the smallest range we can produce, so a range is only splittable while it
        // still spans more than one day (or has not been bounded yet).
        var isSplittable = from is null || to is null || from < to;

        int page = 1;
        int? totalPages = null;
        var totalCount = 0;
        var splitRange = false;
        do
        {
            var queryString = new Dictionary<string, object>()
            {
                { "q", string.Join('+', rangeQuery) },
                { "per_page", ResultsPerPage },
                { "page", page },
                { "sort", "updated" }
            };
            var searchReposUri = BuildUri("/search/repositories", queryString);
            var results = await GetSearchResultsAsync(searchReposUri, cancellationToken);
            if (results == null)
            {
                break;
            }

            totalCount = results.TotalCount;
            // Too many results for this range: split the date range instead of paging past the cap.
            if (totalCount > MaxSearchResults && isSplittable)
            {
                splitRange = true;
                break;
            }

            _logger.LogDebug("Found {Count} repositories for query {Query}", results.Items.Length, searchReposUri);
            foreach (var gitHubRepo in results.Items)
            {
                yield return gitHubRepo;
            }

            totalPages ??= (int)Math.Ceiling(Math.Min(totalCount, MaxSearchResults) / (double)ResultsPerPage);
        } while (page++ < totalPages);

        if (splitRange)
        {
            // Establish concrete bounds the first time we split (GitHub launched in 2008, so no
            // repository predates it), then split [from, to] into two disjoint halves and recurse.
            // Ranges never overlap, so results are unique by construction and need no deduplication.
            var lower = from ?? new DateOnly(2008, 1, 1);
            var upper = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var mid = DateOnly.FromDayNumber(lower.DayNumber + (upper.DayNumber - lower.DayNumber) / 2);
            await foreach (var gitHubRepo in SearchRepositoriesByDateRangeAsync(query, lower, mid, cancellationToken))
            {
                yield return gitHubRepo;
            }

            await foreach (var gitHubRepo in SearchRepositoriesByDateRangeAsync(query, mid.AddDays(1), upper, cancellationToken))
            {
                yield return gitHubRepo;
            }
        }
        else if (totalCount > MaxSearchResults)
        {
            // from == to: a single day holds more than the cap, so it cannot be split further and only
            // the first MaxSearchResults results are reachable.
            _logger.LogWarning("More than {Max} repositories created on {Date} for query {Query}; some results may be missing", MaxSearchResults, from, string.Join('+', query));
        }
    }

    private async Task<GitHubSearchResults?> GetSearchResultsAsync(Uri searchUri, CancellationToken cancellationToken)
    {
        await ThrottleSearchRequestAsync(cancellationToken);
        return await _httpClientFactory.CreateGitHubClient().GetFromJsonAsync<GitHubSearchResults>(searchUri, cancellationToken);
    }

    // Ensures a minimum delay between consecutive Search API requests. The client is a singleton and
    // search requests are issued sequentially, so a single shared gate paces the whole indexing run.
    private async Task ThrottleSearchRequestAsync(CancellationToken cancellationToken)
    {
        await _searchThrottle.WaitAsync(cancellationToken);
        try
        {
            var delay = _nextSearchRequestAt - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            _nextSearchRequestAt = DateTimeOffset.UtcNow + MinSearchInterval;
        }
        finally
        {
            _searchThrottle.Release();
        }
    }

    private static Uri BuildUri(string path, Dictionary<string, object>? queryString = null)
    {
        var uriBuilder = new UriBuilder(GitHubApiBaseUri)
        {
            Path = path,
            Query = queryString == null ? null : string.Join("&", queryString.Select(kv => $"{kv.Key}={kv.Value}"))
        };

        return uriBuilder.Uri;
    }
}
