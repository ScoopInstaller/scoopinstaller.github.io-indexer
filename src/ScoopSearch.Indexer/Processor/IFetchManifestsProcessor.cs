using ScoopSearch.Indexer.Buckets;
using ScoopSearch.Indexer.Data;

namespace ScoopSearch.Indexer.Processor;

public interface IFetchManifestsProcessor
{
    IAsyncEnumerable<ManifestInfo> FetchManifestsAsync(Bucket bucket, CancellationToken cancellationToken);
}
