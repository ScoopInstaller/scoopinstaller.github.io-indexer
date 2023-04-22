using ScoopSearch.Indexer.Data;

namespace ScoopSearch.Indexer.Manifest;

public interface IKeyGenerator
{
    string Generate(ManifestMetadata manifestMetadata);
}
