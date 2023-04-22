using System.Text.Json;
using ScoopSearch.Indexer.Data;

namespace ScoopSearch.Indexer.Tests.Helpers;

public static class ManifestInfoExtensions
{
    public static ManifestInfo ToManifestInfo(this (string Id, string Sha, int RepositoryStars) @this)
    {
        return JsonSerializer.Deserialize<ManifestInfo>(@$"{{ ""Id"": ""{@this.Id}"", ""Metadata"": {{ ""Sha"": ""{@this.Sha}"", ""RepositoryStars"": {@this.RepositoryStars}, ""OfficialRepositoryNumber"": 0 }} }}")!;
    }
}
