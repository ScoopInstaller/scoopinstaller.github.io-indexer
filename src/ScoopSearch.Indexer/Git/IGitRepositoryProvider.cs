namespace ScoopSearch.Indexer.Git;

public interface IGitRepositoryProvider
{
    IGitRepository? Download(Uri uri, CancellationToken cancellationToken);
}
