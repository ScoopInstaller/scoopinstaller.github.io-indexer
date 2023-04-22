using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ScoopSearch.Functions.Data;

namespace ScoopSearch.Functions.Indexer
{
    public interface IIndexer
    {
        Task<IEnumerable<ManifestInfo>> GetExistingManifestsAsync(Uri repository, CancellationToken token);

        Task<IEnumerable<Uri>> GetBucketsAsync(CancellationToken token);

        Task DeleteManifestsAsync(IEnumerable<ManifestInfo> manifests, CancellationToken token);

        Task AddManifestsAsync(IEnumerable<ManifestInfo> manifests, CancellationToken token);
    }
}
