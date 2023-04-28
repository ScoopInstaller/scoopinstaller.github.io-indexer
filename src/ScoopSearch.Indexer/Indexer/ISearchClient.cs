using ScoopSearch.Indexer.Data;

namespace ScoopSearch.Indexer.Indexer;

public interface ISearchClient
{
    Task<IEnumerable<ManifestInfo>> GetExistingManifestsAsync(IEnumerable<Uri> repositories, CancellationToken token);

    Task<IEnumerable<ManifestInfo>> GetAllManifestsAsync(CancellationToken token);

    Task<IEnumerable<Uri>> GetBucketsAsync(CancellationToken token);

    Task DeleteManifestsAsync(IEnumerable<ManifestInfo> manifests, CancellationToken token);

    Task UpsertManifestsAsync(IEnumerable<ManifestInfo> manifests, CancellationToken token);
}
