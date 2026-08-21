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

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubClient> _logger;

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
        var rangeQuery = BuildRangeQuery(query, from, to);

        // The split decision is taken from the first page only, before any result is yielded, so a
        // live count that grows past the cap mid-pagination cannot restart the range and yield the
        // already-returned repositories again.
        var firstPage = await GetSearchResultsAsync(BuildSearchUri(rangeQuery, 1), cancellationToken);
        if (firstPage == null)
        {
            yield break;
        }

        // A single day is the smallest range we can produce, so a range is only splittable while it
        // still spans more than one day (or has not been bounded yet).
        var isSplittable = from is null || to is null || from < to;
        if (firstPage.TotalCount > MaxSearchResults && isSplittable)
        {
            await foreach (var gitHubRepo in SplitAndSearchByDateRangeAsync(query, from, to, cancellationToken))
            {
                yield return gitHubRepo;
            }

            yield break;
        }

        if (firstPage.TotalCount > MaxSearchResults)
        {
            // from == to: a single day holds more than the cap, so it cannot be split further and only
            // the first MaxSearchResults results are reachable.
            _logger.LogWarning("More than {Max} repositories created on {Date} for query {Query}; some results may be missing", MaxSearchResults, from, string.Join('+', rangeQuery));
        }

        var totalPages = (int)Math.Ceiling(Math.Min(firstPage.TotalCount, MaxSearchResults) / (double)ResultsPerPage);
        for (var page = 1; page <= totalPages; page++)
        {
            var results = page == 1 ? firstPage : await GetSearchResultsAsync(BuildSearchUri(rangeQuery, page), cancellationToken);
            if (results == null)
            {
                yield break;
            }

            _logger.LogDebug("Found {Count} repositories on page {Page} for query {Query}", results.Items.Length, page, string.Join('+', rangeQuery));
            foreach (var gitHubRepo in results.Items)
            {
                yield return gitHubRepo;
            }
        }
    }

    // Splits [from, to] into two disjoint halves and recurses. Ranges never overlap, so results are
    // unique by construction and need no deduplication. GitHub's earliest repositories date back to
    // 2007 (e.g. mojombo/grit, created 2007-10-29), so the lower bound must cover them to avoid
    // dropping repositories that the unsplit query would have returned.
    private async IAsyncEnumerable<GitHubRepo> SplitAndSearchByDateRangeAsync(string[] query, DateOnly? from, DateOnly? to, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var lower = from ?? new DateOnly(2007, 1, 1);
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

    private static string[] BuildRangeQuery(string[] query, DateOnly? from, DateOnly? to)
    {
        return from is { } fromDate && to is { } toDate
            ? query.Append($"created:{fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}").ToArray()
            : query;
    }

    private static Uri BuildSearchUri(string[] rangeQuery, int page)
    {
        return BuildUri("/search/repositories", new Dictionary<string, object>()
        {
            { "q", string.Join('+', rangeQuery) },
            { "per_page", ResultsPerPage },
            { "page", page },
            { "sort", "updated" }
        });
    }

    private async Task<GitHubSearchResults?> GetSearchResultsAsync(Uri searchUri, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.CreateGitHubClient().GetFromJsonAsync<GitHubSearchResults>(searchUri, cancellationToken);
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
