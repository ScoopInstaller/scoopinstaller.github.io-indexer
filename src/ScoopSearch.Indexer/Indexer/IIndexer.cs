using ScoopSearch.Indexer.Data;

namespace ScoopSearch.Indexer.Indexer;

public interface IIndexer
{
    Task<IEnumerable<ManifestInfo>> GetExistingManifestsAsync(Uri repository, CancellationToken token);

    Task<IEnumerable<Uri>> GetBucketsAsync(CancellationToken token);

    Task DeleteManifestsAsync(IEnumerable<ManifestInfo> manifests, CancellationToken token);

    Task AddManifestsAsync(IEnumerable<ManifestInfo> manifests, CancellationToken token);
}
