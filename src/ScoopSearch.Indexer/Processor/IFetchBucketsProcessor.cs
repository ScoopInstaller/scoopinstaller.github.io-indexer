using ScoopSearch.Indexer.Data;

namespace ScoopSearch.Indexer.Processor;

public interface IFetchBucketsProcessor
{
    Task<BucketInfo[]> FetchBucketsAsync(CancellationToken cancellationToken);
}
