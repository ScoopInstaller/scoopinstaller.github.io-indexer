using System.Text.Json.Serialization;

namespace ScoopSearch.Indexer.GitHub;

public class GitHubSearchResults
{
    [JsonInclude, JsonPropertyName("total_count")]
    public int TotalCount { get; private set; }

    [JsonInclude, JsonPropertyName("items")]
    public GitHubRepo[] Items { get; private set; } = null!;
}
