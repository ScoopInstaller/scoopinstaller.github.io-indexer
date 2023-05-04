using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.Extensions;

namespace ScoopSearch.Indexer.Manifest;

internal class KeyGenerator : IKeyGenerator
{
    public string Generate(ManifestMetadata manifestMetadata)
    {
        var key = $"{manifestMetadata.Repository}{manifestMetadata.BranchName}{manifestMetadata.FilePath}";

        return key.Sha1Sum();
    }
}
