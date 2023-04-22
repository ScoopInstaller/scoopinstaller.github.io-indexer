using ScoopSearch.Indexer.Data;

namespace ScoopSearch.Indexer.Manifest;

public interface IManifestCrawler
{
    IEnumerable<ManifestInfo> GetManifestsFromRepository(Uri bucketUri, CancellationToken cancellationToken);
}
