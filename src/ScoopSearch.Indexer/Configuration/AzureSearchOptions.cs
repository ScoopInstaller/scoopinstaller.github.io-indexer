namespace ScoopSearch.Indexer.Configuration;

public class AzureSearchOptions
{
    public const string Key = "AzureSearch";

    public Uri ServiceUrl { get; set; } = null!;

    public string AdminApiKey { get; set; } = null!;

    public string IndexName { get; set; } = null!;
}
