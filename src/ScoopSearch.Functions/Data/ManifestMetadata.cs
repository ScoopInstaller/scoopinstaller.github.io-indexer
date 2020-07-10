using System;
using Microsoft.Azure.Search;
using Newtonsoft.Json;
using ScoopSearch.Functions.Indexer;

namespace ScoopSearch.Functions.Data
{
    public class ManifestMetadata
    {
        public const string RepositoryField = nameof(ManifestInfo.Metadata) + "/" + nameof(Repository);
        public const string RepositoryStarsField = nameof(ManifestInfo.Metadata) + "/" + nameof(RepositoryStars);
        public const string ShaField = nameof(ManifestInfo.Metadata) + "/" + nameof(Sha);
        public const string OfficialRepositoryNumberField = nameof(ManifestInfo.Metadata) + "/" + nameof(OfficialRepositoryNumber);

        [JsonConstructor]
        private ManifestMetadata()
        {
        }

        public ManifestMetadata(
            string repository,
            string branchName,
            string filePath,
            string authorName,
            string authorMail,
            DateTimeOffset committed,
            string sha)
        {
            Repository = repository;
            BranchName = branchName;
            FilePath = filePath;
            AuthorName = authorName;
            AuthorMail = authorMail;
            Committed = committed;
            Sha = sha;
        }

        [IsSearchable, IsFilterable, IsSortable, IsFacetable]
        [Analyzer(AzureSearchIndex.UrlAnalyzer)]
        [JsonProperty]
        public string Repository { get; private set; }

        [JsonProperty]
        public bool OfficialRepository { get; private set; }

        // Used for scoring profile
        [IsFilterable, IsSortable, IsFacetable]
        [JsonProperty]
        public int OfficialRepositoryNumber { get; private set; }

        [IsFilterable, IsSortable, IsFacetable]
        [JsonProperty]
        public int RepositoryStars { get; private set; }

        [IsFilterable]
        [JsonProperty]
        public string BranchName { get; private set; }

        [IsFilterable, IsSortable, IsFacetable]
        [JsonProperty]
        public string FilePath { get; private set; }

        [IsSearchable, IsFilterable, IsSortable, IsFacetable]
        [JsonProperty]
        public string AuthorName { get; private set; }

        [IsSearchable, IsFilterable, IsSortable, IsFacetable]
        [JsonProperty]
        public string AuthorMail { get; private set; }

        [IsFilterable, IsSortable, IsFacetable]
        [JsonProperty]
        public DateTimeOffset Committed { get; private set; }

        [IsFilterable]
        [JsonProperty]
        public string Sha { get; private set; }

        public void SetRepositoryMetadata(bool officialRepository, int repositoryStars)
        {
            OfficialRepository = officialRepository;
            OfficialRepositoryNumber = OfficialRepository ? 1 : 0;
            RepositoryStars = repositoryStars;
        }
    }
}
