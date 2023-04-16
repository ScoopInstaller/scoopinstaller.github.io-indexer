using ScoopSearch.Functions.Data;

namespace ScoopSearch.Functions.Manifest
{
    internal class KeyGenerator : IKeyGenerator
    {
        public string Generate(ManifestMetadata manifestMetadata)
        {
            var key = $"{manifestMetadata.Repository}{manifestMetadata.BranchName}{manifestMetadata.FilePath}";

            return key.Sha1Sum();
        }
    }
}
