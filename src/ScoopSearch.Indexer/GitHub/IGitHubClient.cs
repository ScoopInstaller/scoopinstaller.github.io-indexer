namespace ScoopSearch.Indexer.GitHub;

public interface IGitHubClient
{
    Task<GitHubRepo?> GetRepositoryAsync(Uri uri, CancellationToken cancellationToken);

    IAsyncEnumerable<GitHubRepo> SearchRepositoriesAsync(string[] query, CancellationToken cancellationToken);
}
