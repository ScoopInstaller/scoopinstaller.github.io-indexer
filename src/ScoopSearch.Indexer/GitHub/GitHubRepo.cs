using System.Text.Json.Serialization;

namespace ScoopSearch.Indexer.GitHub;

public class GitHubRepo
{
    [JsonInclude, JsonPropertyName("html_url")]
    public Uri HtmlUri { get; private set; } = null!;

    [JsonInclude, JsonPropertyName("stargazers_count")]
    public int Stars { get; private set; }
}
