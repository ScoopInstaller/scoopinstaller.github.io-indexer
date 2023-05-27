using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.GitHub;

namespace ScoopSearch.Indexer.Buckets.Sources;

internal class GitHubBucketsSource : IBucketsSource
{
    private readonly IGitHubClient _gitHubClient;
    private readonly GitHubOptions _gitHubOptions;
    private readonly ILogger _logger;

    public GitHubBucketsSource(
        IGitHubClient gitHubClient,
        IOptions<GitHubOptions> gitHubOptions,
        ILogger<GitHubBucketsSource> logger)
    {
        _gitHubClient = gitHubClient;
        _logger = logger;
        _gitHubOptions = gitHubOptions.Value;
    }

    public async IAsyncEnumerable<Bucket> GetBucketsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_gitHubOptions.BucketsSearchQueries is null || _gitHubOptions.BucketsSearchQueries.Length == 0)
        {
            _logger.LogWarning("No buckets search queries found in configuration");
            yield break;
        }

        foreach (var query in _gitHubOptions.BucketsSearchQueries)
        {
            await foreach(var repo in _gitHubClient.SearchRepositoriesAsync(query, cancellationToken))
            {
                yield return new Bucket(repo.HtmlUri, repo.Stars);
            }
        }
    }
}
