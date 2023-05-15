namespace ScoopSearch.Indexer.GitHub;

public interface IGitHubClient
{
    Task<string> GetAsStringAsync(Uri uri, CancellationToken cancellationToken);

    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool followRedirects, CancellationToken cancellationToken);

    Task<GitHubRepo?> GetRepositoryAsync(Uri uri, CancellationToken cancellationToken);

    bool IsValidRepositoryDomain(Uri uri);

    IAsyncEnumerable<GitHubRepo> SearchRepositoriesAsync(Uri query, CancellationToken cancellationToken);
}
