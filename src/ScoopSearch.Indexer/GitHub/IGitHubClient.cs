namespace ScoopSearch.Indexer.GitHub;

public interface IGitHubClient
{
    Task<string> GetAsStringAsync(Uri uri, CancellationToken cancellationToken);

    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool followRedirects, CancellationToken cancellationToken);

    Task<GitHubRepo?> GetRepoAsync(Uri uri, CancellationToken cancellationToken);

    Task<GitHubSearchResults?> GetSearchResultsAsync(Uri searchUri, CancellationToken cancellationToken);
}
