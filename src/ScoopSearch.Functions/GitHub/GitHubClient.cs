using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ScoopSearch.Functions.Data;

namespace ScoopSearch.Functions.GitHub;

internal class GitHubClient : IGitHubClient
{
    private readonly ILogger<GitHubClient> _logger;
    private const string GitHubApiRepoBaseUri = "https://api.github.com/repos";

    private readonly HttpClient _githubHttpClient;
    private readonly HttpClient _githubHttpClientNoRedirect;

    public GitHubClient(
        IHttpClientFactory httpClientFactory,
        ILogger<GitHubClient> logger)
    {
        _githubHttpClient = httpClientFactory.CreateClient(Constants.GitHubHttpClientName);
        _githubHttpClientNoRedirect = httpClientFactory.CreateClient(Constants.GitHubHttpClientNoRedirectName);
        _logger = logger;
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

    public async Task<GitHubRepo?> GetRepoAsync(Uri uri, CancellationToken cancellationToken)
    {
        var apiRepoUri = new Uri(GitHubApiRepoBaseUri + uri.PathAndQuery);
        return await GetAsStringAsync(apiRepoUri, cancellationToken)
            .ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    return JsonConvert.DeserializeObject<GitHubRepo>(task.Result);
                }
                else
                {
                    return null;
                }
            }, cancellationToken);
    }

    public async Task<GitHubSearchResults> GetSearchResultsAsync(Uri searchUri, CancellationToken cancellationToken)
    {
        return await GetAsStringAsync(searchUri, cancellationToken)
            .ContinueWith(task
                => JsonConvert.DeserializeObject<GitHubSearchResults>(task.Result), cancellationToken);
    }
}
