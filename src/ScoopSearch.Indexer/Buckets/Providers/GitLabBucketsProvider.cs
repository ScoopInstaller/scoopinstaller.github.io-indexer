using ScoopSearch.Indexer.GitLab;

namespace ScoopSearch.Indexer.Buckets.Providers;

internal class GitLabBucketsProvider : IBucketsProvider
{
    private const string GitLabDomain = "gitlab.com";

    private readonly IGitLabClient _gitLabClient;

    public GitLabBucketsProvider(IGitLabClient gitLabClient)
    {
        _gitLabClient = gitLabClient;
    }

    public async Task<Bucket?> GetBucketAsync(Uri uri, CancellationToken cancellationToken)
    {
        var result = await _gitLabClient.GetRepositoryAsync(uri, cancellationToken);
        if (result is not null)
        {
            return new Bucket(result.WebUrl, result.Stars);
        }

        return null;
    }

    public bool IsCompatible(Uri uri) => uri.Host.EndsWith(GitLabDomain, StringComparison.Ordinal);

}
