namespace ScoopSearch.Indexer.Buckets.Sources;

public interface IBucketsSource
{
    IAsyncEnumerable<Bucket> GetBucketsAsync(CancellationToken cancellationToken);
}
