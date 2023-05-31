namespace ScoopSearch.Indexer.Configuration;

public class GitHubOptions
{
    public const string Key = "GitHub";

    public string? Token { get; set; }

    public string[][]? BucketsSearchQueries { get; set; }
}
