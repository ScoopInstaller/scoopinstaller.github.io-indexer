using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ScoopSearch.Indexer.GitHub;

internal class GitHubClient : IGitHubClient
{
    private const string GitHubApiRepoBaseUri = "https://api.github.com/repos";
    private const string GitHubDomain = "github.com";
    private const int ResultsPerPage = 100;

    private readonly HttpClient _githubHttpClient;

    public GitHubClient(IHttpClientFactory httpClientFactory)
    {
        _githubHttpClient = httpClientFactory.CreateClient(Constants.GitHubHttpClientName);
    }

    public async Task<string> GetAsStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
        using (var response = await _githubHttpClient.SendAsync(request, cancellationToken))
        {
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _githubHttpClient.SendAsync(request, cancellationToken);
    }

    public async Task<GitHubRepo?> GetRepositoryAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!IsValidRepositoryDomain(uri))
        {
            throw new ArgumentException("The URI must be a GitHub repo URI", nameof(uri));
        }

        var apiRepoUri = new Uri(GitHubApiRepoBaseUri + uri.PathAndQuery);
        return await GetAsStringAsync(apiRepoUri, cancellationToken)
            .ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    return JsonSerializer.Deserialize<GitHubRepo>(task.Result);
                }
                else
                {
                    return null;
                }
            }, cancellationToken);
    }

    public bool IsValidRepositoryDomain(Uri uri)
    {
        return uri.Host.EndsWith(GitHubDomain, StringComparison.Ordinal);
    }

    public async IAsyncEnumerable<GitHubRepo> SearchRepositoriesAsync(Uri query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int page = 1;
        int? totalPages = null;
        do
        {
            var searchUri = new Uri($"{query.AbsoluteUri}&per_page={ResultsPerPage}&page={page}&sort=updated");
            var results = await GetSearchResultsAsync(searchUri, cancellationToken);
            if (results == null)
            {
                break;
            }

            foreach (var gitHubRepo in results.Items)
            {
                yield return gitHubRepo;
            }

            totalPages ??= (int)Math.Ceiling(results.TotalCount / (double)ResultsPerPage);
        } while (page++ < totalPages);
    }

    private async Task<GitHubSearchResults?> GetSearchResultsAsync(Uri searchUri, CancellationToken cancellationToken)
    {
        return await GetAsStringAsync(searchUri, cancellationToken)
            .ContinueWith(task => JsonSerializer.Deserialize<GitHubSearchResults>(task.Result), cancellationToken);
    }
}
