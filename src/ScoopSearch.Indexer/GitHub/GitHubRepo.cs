using System.Text.Json.Serialization;

namespace ScoopSearch.Indexer.GitHub;

public class GitHubRepo
{
    public GitHubRepo()
    {
    }

    internal GitHubRepo(Uri htmlUri, int stars)
    {
        HtmlUri = htmlUri;
        Stars = stars;
    }

    [JsonInclude, JsonPropertyName("html_url")]
    public Uri HtmlUri { get; private set; } = null!;

    [JsonInclude, JsonPropertyName("stargazers_count")]
    public int Stars { get; private set; }
}
