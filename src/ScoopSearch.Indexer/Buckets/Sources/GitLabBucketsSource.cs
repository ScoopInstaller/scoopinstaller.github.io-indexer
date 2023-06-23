using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.GitLab;

namespace ScoopSearch.Indexer.Buckets.Sources;

internal class GitLabBucketsSource : IBucketsSource
{
    private readonly IGitLabClient _gitLabClient;
    private readonly GitLabOptions _gitLabOptions;
    private readonly ILogger _logger;

    public GitLabBucketsSource(
        IGitLabClient gitLabClient,
        IOptions<GitLabOptions> gitLabOptions,
        ILogger<GitLabBucketsSource> logger)
    {
        _gitLabClient = gitLabClient;
        _logger = logger;
        _gitLabOptions = gitLabOptions.Value;
    }

    public async IAsyncEnumerable<Bucket> GetBucketsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_gitLabOptions.BucketsSearchQueries is null || _gitLabOptions.BucketsSearchQueries.Length == 0)
        {
            _logger.LogWarning("No buckets search queries found in configuration");
            yield break;
        }

        foreach (var query in _gitLabOptions.BucketsSearchQueries)
        {
            await foreach(var repo in _gitLabClient.SearchRepositoriesAsync(query, cancellationToken))
            {
                yield return new Bucket(repo.WebUrl, repo.Stars);
            }
        }
    }
}
