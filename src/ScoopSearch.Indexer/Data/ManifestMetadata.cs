using System.Text.Json.Serialization;
using Azure.Search.Documents.Indexes;
using ScoopSearch.Indexer.Indexer;

namespace ScoopSearch.Indexer.Data;

public class ManifestMetadata
{
    public const string RepositoryField = nameof(ManifestInfo.Metadata) + "/" + nameof(Repository);
    public const string RepositoryStarsField = nameof(ManifestInfo.Metadata) + "/" + nameof(RepositoryStars);
    public const string ShaField = nameof(ManifestInfo.Metadata) + "/" + nameof(Sha);
    public const string CommittedField = nameof(ManifestInfo.Metadata) + "/" + nameof(Committed);
    public const string FilePathField = nameof(ManifestInfo.Metadata) + "/" + nameof(FilePath);
    public const string OfficialRepositoryNumberField = nameof(ManifestInfo.Metadata) + "/" + nameof(OfficialRepositoryNumber);
    public const string DuplicateOfField = nameof(ManifestInfo.Metadata) + "/" + nameof(DuplicateOf);

    public ManifestMetadata()
    {
    }

    public ManifestMetadata(
        string repository,
        string branchName,
        string filePath,
        string authorName,
        string authorMail,
        DateTimeOffset committed,
        string sha,
        string manifestHash)
    {
        Repository = repository;
        BranchName = branchName;
        FilePath = filePath;
        AuthorName = authorName;
        AuthorMail = authorMail;
        Committed = committed;
        Sha = sha;
        ManifestHash = manifestHash;
    }

    [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true, AnalyzerName = AzureSearchIndex.UrlAnalyzer)]
    [JsonInclude]
    public string Repository { get; private set; } = null!;

    [SimpleField]
    [JsonInclude]
    public bool? OfficialRepository { get; private set; }

    // Used for scoring profile
    [SimpleField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
    [JsonInclude]
    public int? OfficialRepositoryNumber { get; private set; }

    [SimpleField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
    [JsonInclude]
    public int RepositoryStars { get; private set; }

    [SimpleField(IsFilterable = true)]
    [JsonInclude]
    public string? BranchName { get; private set; }

    [SimpleField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
    [JsonInclude]
    public string? FilePath { get; private set; }

    [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
    [JsonInclude]
    public string? AuthorName { get; private set; }

    [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
    [JsonInclude]
    public string? AuthorMail { get; private set; }

    [SimpleField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
    [JsonInclude]
    public DateTimeOffset? Committed { get; private set; }

    [SimpleField(IsFilterable = true)]
    [JsonInclude]
    public string Sha { get; private set; } = null!;

    [JsonIgnore]
    public string ManifestHash { get; private set; } = null!;

    [SimpleField(IsFilterable = true)]
    [JsonInclude]
    public string? DuplicateOf { get; private set; }

    public void SetRepositoryMetadata(bool officialRepository, int repositoryStars)
    {
        OfficialRepository = officialRepository;
        OfficialRepositoryNumber = OfficialRepository.GetValueOrDefault() ? 1 : 0;
        RepositoryStars = repositoryStars;
    }

    public void SetDuplicateOf(string originalId)
    {
        DuplicateOf = originalId;
    }
}
