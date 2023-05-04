using ScoopSearch.Indexer.Data;

namespace ScoopSearch.Indexer.Processor;

public interface IIndexingProcessor
{
    Task CreateIndexIfRequiredAsync(CancellationToken cancellationToken);

    Task CleanIndexFromNonExistentBucketsAsync(Uri[] buckets, CancellationToken cancellationToken);

    Task UpdateIndexWithManifestsAsync(ManifestInfo[] manifestsFromRepositories, CancellationToken cancellationToken);
}
