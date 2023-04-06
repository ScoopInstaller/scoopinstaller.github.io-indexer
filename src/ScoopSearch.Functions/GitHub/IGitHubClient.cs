using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ScoopSearch.Functions.Data;

namespace ScoopSearch.Functions.GitHub;

public interface IGitHubClient
{
    Task<string> GetAsStringAsync(Uri uri, CancellationToken cancellationToken);

    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool followRedirects, CancellationToken cancellationToken);

    Task<GitHubRepo?> GetRepoAsync(Uri uri, CancellationToken cancellationToken);

    Task<GitHubSearchResults?> GetSearchResultsAsync(Uri searchUri, CancellationToken cancellationToken);
}
