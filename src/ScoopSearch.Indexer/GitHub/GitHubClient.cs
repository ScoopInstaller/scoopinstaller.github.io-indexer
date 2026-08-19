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
        // A single query can match more than the MaxSearchResults the Search API is willing to return.
        // The repositories endpoint cannot sort by creation date, so we cannot page past the cap with a
        // cursor. Instead we recursively split the query into disjoint creation-date ranges until each
        // range holds at most MaxSearchResults. GitHub launched in 2008, so no repository predates it.
        var from = new DateOnly(2008, 1, 1);
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        return SearchRepositoriesByDateRangeAsync(query, from, to, cancellationToken);
    }

    private async IAsyncEnumerable<GitHubRepo> SearchRepositoriesByDateRangeAsync(string[] query, DateOnly from, DateOnly to, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rangeQuery = query.Append($"created:{from:yyyy-MM-dd}..{to:yyyy-MM-dd}").ToArray();

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
            if (totalCount > MaxSearchResults && from < to)
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
            // Split [from, to] into two disjoint halves and recurse. Ranges never overlap, so results
            // are unique by construction and require no deduplication.
            var mid = DateOnly.FromDayNumber(from.DayNumber + (to.DayNumber - from.DayNumber) / 2);
            await foreach (var gitHubRepo in SearchRepositoriesByDateRangeAsync(query, from, mid, cancellationToken))
            {
                yield return gitHubRepo;
            }

            await foreach (var gitHubRepo in SearchRepositoriesByDateRangeAsync(query, mid.AddDays(1), to, cancellationToken))
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
