using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ScoopSearch.Indexer.Extensions;

namespace ScoopSearch.Indexer.GitHub;

internal class GitHubClient : IGitHubClient
{
    private const string GitHubApiBaseUri = "https://api.github.com/";
    private const int ResultsPerPage = 100;

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
        return await _httpClientFactory.CreateGitHubClient().GetStringAsync(getRepoUri, cancellationToken)
            .ContinueWith(task => task.Deserialize<GitHubRepo>(), cancellationToken);
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

    public async IAsyncEnumerable<GitHubRepo> SearchRepositoriesAsync(string[] query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int page = 1;
        int? totalPages = null;
        do
        {
            var queryString = new Dictionary<string, object>()
            {
                { "q", string.Join('+', query) },
                { "per_page", ResultsPerPage },
                { "page", page },
                { "sort", "updated" }
            };
            var searchReposUri = BuildUri("/search/repositories", queryString);
            var results = await GetSearchResultsAsync(searchReposUri, cancellationToken);
            if (results == null)
            {
                yield break;
            }

            _logger.LogDebug("Found {Count} repositories for query {Query}", results.Items.Length, searchReposUri);
            foreach (var gitHubRepo in results.Items)
            {
                yield return gitHubRepo;
            }

            totalPages ??= (int)Math.Ceiling(results.TotalCount / (double)ResultsPerPage);
        } while (page++ < totalPages);
    }

    private async Task<GitHubSearchResults?> GetSearchResultsAsync(Uri searchUri, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.CreateGitHubClient().GetStringAsync(searchUri, cancellationToken)
            .ContinueWith(task => task.Deserialize<GitHubSearchResults>(), cancellationToken);
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

    private class GitHubSearchResults
    {
        [JsonInclude, JsonPropertyName("total_count")]
        public int TotalCount { get; private set; }

        [JsonInclude, JsonPropertyName("items")]
        public GitHubRepo[] Items { get; private set; } = null!;
    }
}
