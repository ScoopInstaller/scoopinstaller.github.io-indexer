namespace ScoopSearch.Indexer.Indexer;

public interface ISearchIndex
{
    Task CreateIndexIfRequiredAsync(CancellationToken cancellationToken);
}
