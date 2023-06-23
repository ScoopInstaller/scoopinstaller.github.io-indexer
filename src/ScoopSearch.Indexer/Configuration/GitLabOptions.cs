namespace ScoopSearch.Indexer.Configuration;

public class GitLabOptions
{
    public const string Key = "GitLab";

    public string? Token { get; set; }

    public string[]? BucketsSearchQueries { get; set; }
}
