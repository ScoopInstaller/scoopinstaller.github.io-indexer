using System.Runtime.CompilerServices;
using System.Text.Json;
using Octokit.GraphQL;
using Octokit.GraphQL.Core;
using Octokit.GraphQL.Model;

namespace ScoopSearch.Indexer.GitHub;

internal class GitHubClient : IGitHubClient
{
    private const string GitHubApiRepoBaseUri = "https://api.github.com/repos";
    private const string GitHubDomain = "github.com";

    private readonly HttpClient _githubHttpClient;
    private readonly HttpClient _githubHttpClientNoRedirect;
    private readonly Lazy<Connection> _graphQLConnection;

    public GitHubClient(IHttpClientFactory httpClientFactory)
    {
        _githubHttpClient = httpClientFactory.CreateClient(Constants.GitHubHttpClientName);
        _githubHttpClientNoRedirect = httpClientFactory.CreateClient(Constants.GitHubHttpClientNoRedirectName);

        var userAgent = _githubHttpClient.DefaultRequestHeaders.UserAgent.Single().Product!;
        _graphQLConnection = new Lazy<Connection>(() => new Connection(
            new ProductHeaderValue(userAgent.Name, userAgent.Version),
            _githubHttpClient.DefaultRequestHeaders.Authorization!.Parameter));
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

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool followRedirects, CancellationToken cancellationToken)
    {
        return (followRedirects ? _githubHttpClient : _githubHttpClientNoRedirect).SendAsync(request, cancellationToken);
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

    public async IAsyncEnumerable<GitHubRepo> SearchRepositoriesAsync(string query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var compiledSearchQuery = new Query()
            .Search(new Arg<string>("query", false), SearchType.Repository, first: 100, after: new Arg<string>("after", true))
            .Select(connection => new
            {
                EndCursor = connection.PageInfo.EndCursor,
                HasNextPage = connection.PageInfo.HasNextPage,
                TotalCount = connection.RepositoryCount,
                Repos = connection.Nodes.OfType<Repository>().Select(item => new { Url = new Uri(item.Url), Stars = item.StargazerCount }).ToList()
            })
            .Compile();

        var vars = new Dictionary<string, object?>
        {
            { "query", query },
            { "after", null },
        };

        do
        {
            var page = await _graphQLConnection.Value.Run(compiledSearchQuery, vars, cancellationToken);
            foreach (var repo in page.Repos)
            {
                yield return new GitHubRepo(repo.Url, repo.Stars);
            }

            vars["after"] = page.HasNextPage ? page.EndCursor : null;

        } while (vars["after"] != null);
    }
}
