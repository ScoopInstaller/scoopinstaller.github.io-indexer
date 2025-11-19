using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoopSearch.Indexer.Buckets;
using ScoopSearch.Indexer.Buckets.Sources;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.Processor;

namespace ScoopSearch.Indexer;

internal class ScoopSearchIndexer : IScoopSearchIndexer
{
    private readonly IEnumerable<IBucketsSource> _bucketsProviders;
    private readonly IOfficialBucketsSource _officialBucketsSource;
    private readonly IFetchManifestsProcessor _fetchManifestsProcessor;
    private readonly IIndexingProcessor _indexingProcessor;
    private readonly BucketsOptions _bucketsOptions;
    private readonly ILogger<ScoopSearchIndexer> _logger;

    public ScoopSearchIndexer(
        IEnumerable<IBucketsSource> bucketsProviders,
        IOfficialBucketsSource officialBucketsSource,
        IFetchManifestsProcessor fetchManifestsProcessor,
        IIndexingProcessor indexingProcessor,
        IOptions<BucketsOptions> bucketsOptions,
        ILogger<ScoopSearchIndexer> logger)
    {
        _bucketsProviders = bucketsProviders;
        _officialBucketsSource = officialBucketsSource;
        _fetchManifestsProcessor = fetchManifestsProcessor;
        _indexingProcessor = indexingProcessor;
        _bucketsOptions = bucketsOptions.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var (allBuckets, allManifests) = await ProcessBucketsAsync(cancellationToken);
        _logger.LogInformation("Found {Buckets} buckets for a total of {Manifests} manifests.", allBuckets.Count, allManifests.Count);

        await _indexingProcessor.CreateIndexIfRequiredAsync(cancellationToken);
        await _indexingProcessor.CleanIndexFromNonExistentBucketsAsync(allBuckets.Select(x => x.Uri).ToArray(), cancellationToken);
        await _indexingProcessor.UpdateIndexWithManifestsAsync(allManifests.ToArray(), cancellationToken);
    }

    private async Task<(ConcurrentBag<Bucket> allBuckets, ConcurrentBag<ManifestInfo> allManifests)> ProcessBucketsAsync(CancellationToken cancellationToken)
    {
        var officialBuckets = await _officialBucketsSource
            .GetBucketsAsync(cancellationToken)
            .ToArrayAsync(cancellationToken);

        var ignoredBuckets = _bucketsOptions.IgnoredBuckets?.Select(uri => uri.AbsoluteUri.ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();
        var buckets = _bucketsProviders.ToAsyncEnumerable()
            .Where(bucketSource => bucketSource is not IOfficialBucketsSource)
            .Select(provider => provider.GetBucketsAsync(cancellationToken))
            .Prepend(officialBuckets.ToAsyncEnumerable())
            .SelectMany(x => x)
            .Distinct(new BucketEqualityComparer())
            .Where(bucket => !ignoredBuckets.Contains(bucket.Uri.AbsoluteUri.ToLowerInvariant()));

        var officialBucketsHashSet = officialBuckets.Select(bucket => bucket.Uri).ToHashSet();
        var allManifests = new ConcurrentBag<ManifestInfo>();
        var allBuckets = new ConcurrentBag<Bucket>();
        await Parallel.ForEachAsync(buckets, cancellationToken, async (bucket, token) =>
        {
            int manifestsCount = 0;
            var stopWatch = Stopwatch.StartNew();
            var isOfficialBuckets = officialBucketsHashSet.Contains(bucket.Uri);
            await foreach (var manifest in _fetchManifestsProcessor.FetchManifestsAsync(bucket, token))
            {
                manifest.Metadata.SetRepositoryMetadata(isOfficialBuckets, bucket.Stars);
                allManifests.Add(manifest);
                manifestsCount++;
            }

            allBuckets.Add(bucket);
            stopWatch.Stop();
            if (manifestsCount == 0)
            {
                _logger.LogInformation("Processed bucket {Uri} (No manifest found, Duration: {Duration:g})", bucket.Uri, stopWatch.Elapsed);
            }
            else
            {
                _logger.LogInformation("Processed bucket {Uri} (Manifests: {Manifests}, Stars: {Stars}, Official: {Official}, Duration: {Duration:g})", bucket.Uri, manifestsCount, bucket.Stars, isOfficialBuckets, stopWatch.Elapsed);
            }
        });

        return (allBuckets, allManifests);
    }

    private class BucketEqualityComparer : IEqualityComparer<Bucket>
    {
        public bool Equals(Bucket? x, Bucket? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null)
            {
                return false;
            }

            if (y is null)
            {
                return false;
            }

            return x.Uri.AbsoluteUri.Equals(y.Uri.AbsoluteUri, StringComparison.InvariantCultureIgnoreCase);
        }

        public int GetHashCode(Bucket obj)
        {
            return obj.Uri.AbsoluteUri.ToLowerInvariant().GetHashCode();
        }
    }
}
