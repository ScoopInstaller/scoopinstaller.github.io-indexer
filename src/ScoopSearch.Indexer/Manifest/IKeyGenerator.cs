using ScoopSearch.Indexer.Data;

namespace ScoopSearch.Indexer.Manifest;

internal interface IKeyGenerator
{
    string Generate(ManifestMetadata manifestMetadata);
}
