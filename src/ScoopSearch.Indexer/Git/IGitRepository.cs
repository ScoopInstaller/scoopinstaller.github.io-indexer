namespace ScoopSearch.Indexer.Git;

public interface IGitRepository
{
    void Delete();

    IReadOnlyDictionary<string, IReadOnlyCollection<CommitInfo>> GetCommitsCache(Predicate<string> filter, CancellationToken cancellationToken);

    string GetBranchName();

    IEnumerable<string> GetFilesFromIndex();

    string ReadContent(string filePath);
}
