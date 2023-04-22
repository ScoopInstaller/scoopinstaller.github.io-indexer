using ScoopSearch.Functions.Data;

namespace ScoopSearch.Functions.Manifest
{
    public interface IKeyGenerator
    {
        string Generate(ManifestMetadata manifestMetadata);
    }
}
