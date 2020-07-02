using System.IO;
using System.Runtime.Serialization;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Newtonsoft.Json;
using ScoopSearch.Functions.Data.JsonConverter;

namespace ScoopSearch.Functions.Data
{
    public class ManifestInfo
    {
        public const string IdField = nameof(Id);
        public const string NameField = nameof(Name);
        public const string DescriptionField = nameof(Description);

        [JsonConstructor]
        private ManifestInfo()
        {
        }

        [System.ComponentModel.DataAnnotations.Key]
        [IsFilterable, IsSortable]
        [JsonProperty]
        public string Id { get; private set; }

        [IsSearchable]
        [JsonProperty]
        public string Name { get; private set; }

        [IsSearchable, IsSortable]
        [JsonProperty]
        public string NameNormalized { get; private set; }

        [IsSearchable]
        [Analyzer(AnalyzerName.AsString.EnLucene)]
        [JsonConverter(typeof(DescriptionConverter))]
        [JsonProperty]
        public string Description { get; private set; }

        [IsFilterable, IsSortable, IsFacetable]
        [JsonProperty]
        public string Homepage { get; private set; }

        [IsSearchable, IsFilterable, IsFacetable]
        [JsonConverter(typeof(LicenseConverter))]
        [JsonProperty]
        public string License { get; private set; }

        [IsSearchable, IsSortable, IsFilterable]
        [JsonProperty]
        public string Version { get; private set; }

        [JsonProperty]
        public ManifestMetadata Metadata { get; private set; }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext context)
        {
            if (context.Context is (string key, ManifestMetadata manifestMetadata))
            {
                Id = key;
                Name = Path.GetFileNameWithoutExtension(manifestMetadata.FilePath);
                NameNormalized = Name.ToLowerInvariant();
                Metadata = manifestMetadata;
            }
        }
    }
}
