using System.IO;
using System.Runtime.Serialization;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Newtonsoft.Json;
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

        [JsonConstructor]
        private ManifestInfo()
        {
        }

        [System.ComponentModel.DataAnnotations.Key]
        [IsFilterable, IsSortable]
        [JsonProperty]
        public string Id { get; private set; } = null!;

        [IsSearchable]
        [JsonProperty]
        [Analyzer(AzureSearchIndex.StandardAnalyzer)]
        public string Name { get; private set; } = null!;

        [IsSearchable, IsSortable]
        [JsonProperty]
        public string NameSortable { get; private set; } = null!;

        [IsSearchable]
        [SearchAnalyzer(AzureSearchIndex.StandardAnalyzer)]
        [IndexAnalyzer(AzureSearchIndex.PrefixAnalyzer)]
        [JsonProperty]
        public string NamePartial { get; private set; } = null!;

        [IsSearchable]
        [SearchAnalyzer(AzureSearchIndex.ReverseAnalyzer)]
        [IndexAnalyzer(AzureSearchIndex.SuffixAnalyzer)]
        [JsonProperty]
        public string NameSuffix { get; private set; } = null!;

        [IsSearchable]
        [Analyzer(AnalyzerName.AsString.EnLucene)]
        [JsonConverter(typeof(DescriptionConverter))]
        [JsonProperty]
        public string Description { get; private set; } = null!;

        [IsSearchable, IsFilterable, IsSortable, IsFacetable]
        [Analyzer(AzureSearchIndex.UrlAnalyzer)]
        [JsonProperty]
        public string Homepage { get; private set; } = null!;

        [IsSearchable, IsFilterable, IsFacetable]
        [JsonConverter(typeof(LicenseConverter))]
        [JsonProperty]
        public string License { get; private set; } = null!;

        [IsSearchable, IsSortable, IsFilterable]
        [Analyzer(AnalyzerName.AsString.Keyword)]
        [JsonProperty]
        public string Version { get; private set; } = null!;

        [JsonProperty]
        public ManifestMetadata Metadata { get; private set; } = null!;

        [OnDeserialized]
        internal void OnDeserialized(StreamingContext context)
        {
            if (context.Context is (string key, ManifestMetadata manifestMetadata))
            {
                Id = key;
                Name = Path.GetFileNameWithoutExtension(manifestMetadata.FilePath);
                NamePartial = Name;
                NameSuffix = Name;
                NameSortable = Name.ToLowerInvariant();
                Metadata = manifestMetadata;
            }
        }
    }
}
