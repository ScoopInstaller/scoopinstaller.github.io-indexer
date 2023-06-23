using System.Text.Json.Serialization;

namespace ScoopSearch.Indexer.GitLab;

public class GitLabRepo
{
    [JsonInclude, JsonPropertyName("web_url")]
    public Uri WebUrl { get; private set; } = null!;

    [JsonInclude, JsonPropertyName("star_count")]
    public int Stars { get; private set; }
}
