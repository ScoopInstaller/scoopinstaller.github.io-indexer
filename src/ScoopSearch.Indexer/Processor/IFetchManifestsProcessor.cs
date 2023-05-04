using ScoopSearch.Indexer.Data;

namespace ScoopSearch.Indexer.Processor;

public interface IFetchManifestsProcessor
{
    Task<ManifestInfo[]> FetchManifestsAsync(BucketInfo bucketInfo, CancellationToken cancellationToken);
}
