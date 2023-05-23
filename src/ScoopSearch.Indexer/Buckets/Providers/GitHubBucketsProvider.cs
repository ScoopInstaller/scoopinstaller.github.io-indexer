using ScoopSearch.Indexer.GitHub;

namespace ScoopSearch.Indexer.Buckets.Providers;

internal class GitHubBucketsProvider : IBucketsProvider
{
    private const string GitHubDomain = "github.com";

    private readonly IGitHubClient _gitHubClient;

    public GitHubBucketsProvider(IGitHubClient gitHubClient)
    {
        _gitHubClient = gitHubClient;
    }

    public async Task<Bucket?> GetBucketAsync(Uri uri, CancellationToken cancellationToken)
    {
        var result = await _gitHubClient.GetRepositoryAsync(uri, cancellationToken);
        if (result is not null)
        {
            return new Bucket(result.HtmlUri, result.Stars);
        }

        return null;
    }

    public bool IsCompatible(Uri uri) => uri.Host.EndsWith(GitHubDomain, StringComparison.Ordinal);
}
