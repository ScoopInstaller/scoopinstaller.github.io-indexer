using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoopSearch.Indexer.Buckets.Providers;
using ScoopSearch.Indexer.Configuration;

namespace ScoopSearch.Indexer.Buckets.Sources;

internal class ManualBucketsSource : IBucketsSource
{
    private readonly IEnumerable<IBucketsProvider> _bucketsProviders;
    private readonly BucketsOptions _bucketOptions;
    private readonly ILogger _logger;

    public ManualBucketsSource(
        IEnumerable<IBucketsProvider> bucketsProviders,
        IOptions<BucketsOptions> bucketOptions,
        ILogger<ManualBucketsSource> logger)
    {
        _bucketsProviders = bucketsProviders;
        _bucketOptions = bucketOptions.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<Bucket> GetBucketsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_bucketOptions.ManualBuckets is null)
        {
            _logger.LogWarning("No manual buckets found in configuration");
            yield break;
        }

        foreach (var uri in _bucketOptions.ManualBuckets)
        {
            var provider = _bucketsProviders.FirstOrDefault(provider => provider.IsCompatible(uri));
            if (provider is not null && await provider.GetBucketAsync(uri, cancellationToken) is { } bucket)
            {
                yield return bucket;
            }
        }
    }
}
