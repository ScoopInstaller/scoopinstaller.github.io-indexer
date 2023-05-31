namespace ScoopSearch.Indexer.Git;

public interface IGitRepository
{
    void Delete();

    Task<IReadOnlyDictionary<string, IReadOnlyCollection<CommitInfo>>> GetCommitsCacheAsync(Predicate<string> filter, CancellationToken cancellationToken);

    string GetBranchName();

    IEnumerable<string> GetFilesFromIndex();

    Task<string> ReadContentAsync(string filePath, CancellationToken cancellationToken);
}
