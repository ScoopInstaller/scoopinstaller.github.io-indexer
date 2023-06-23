namespace ScoopSearch.Indexer.GitLab;

public interface IGitLabClient
{
    Task<GitLabRepo?> GetRepositoryAsync(Uri uri, CancellationToken cancellationToken);

    IAsyncEnumerable<GitLabRepo> SearchRepositoriesAsync(string query, CancellationToken cancellationToken);
}
