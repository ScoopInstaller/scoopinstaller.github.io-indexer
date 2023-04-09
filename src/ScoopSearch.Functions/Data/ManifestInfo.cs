using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using ScoopSearch.Functions.Data.JsonConverter;
using ScoopSearch.Functions.Indexer;

namespace ScoopSearch.Functions.Data
{
    public class ManifestInfo
    {
        public const string IdField = nameof(Id);
        public const string NamePartialField = nameof(NamePartial);
        public const string NameSuffixField = nameof(NameSuffix);
        public const string DescriptionField = nameof(Description);

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ManifestInfo()
        {
        }

        [SimpleField(IsKey = true, IsFilterable = true, IsSortable = true)]
        [JsonInclude]
        public string Id { get; private set; } = null!;

        [SearchableField(AnalyzerName = AzureSearchIndex.StandardAnalyzer)]
        [JsonInclude]
        public string? Name { get; private set; }

        [SearchableField(IsSortable = true)]
        [JsonInclude]
        public string? NameSortable { get; private set; }

        [SearchableField(SearchAnalyzerName = AzureSearchIndex.StandardAnalyzer, IndexAnalyzerName = AzureSearchIndex.PrefixAnalyzer)]
        [JsonInclude]
        public string? NamePartial { get; private set; }

        [SearchableField(SearchAnalyzerName = AzureSearchIndex.ReverseAnalyzer, IndexAnalyzerName = AzureSearchIndex.SuffixAnalyzer)]
        [JsonInclude]
        public string? NameSuffix { get; private set; }

        [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.EnLucene)]
        [JsonConverter(typeof(DescriptionConverter))]
        [JsonInclude]
        public string? Description { get; private set; }

        [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true, AnalyzerName = AzureSearchIndex.UrlAnalyzer)]
        [JsonInclude]
        public string? Homepage { get; private set; }

        [SearchableField(IsFilterable = true, IsFacetable = true)]
        [JsonConverter(typeof(LicenseConverter))]
        [JsonInclude]
        public string? License { get; private set; }

        [SearchableField(IsSortable = true, IsFilterable = true, AnalyzerName = LexicalAnalyzerName.Values.Keyword)]
        [JsonInclude]
        public string? Version { get; private set; }

        [JsonInclude]
        public ManifestMetadata Metadata { get; private set; } = null!;

        internal static ManifestInfo? Deserialize(string contentJson, string key, ManifestMetadata manifestMetadata)
        {
            var manifestInfo = JsonSerializer.Deserialize<ManifestInfo>(contentJson, JsonOptions);
            if (manifestInfo != null)
            {
                manifestInfo.Id = key;
                manifestInfo.Name = Path.GetFileNameWithoutExtension(manifestMetadata.FilePath);
                manifestInfo.NamePartial = manifestInfo.Name;
                manifestInfo.NameSuffix = manifestInfo.Name;
                manifestInfo.NameSortable = manifestInfo.Name?.ToLowerInvariant();
                manifestInfo.Metadata = manifestMetadata;
            }

            return manifestInfo;
        }
    }
}
