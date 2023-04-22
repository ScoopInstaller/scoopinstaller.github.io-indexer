using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.Indexer;
using ScoopSearch.Indexer.Manifest;

namespace ScoopSearch.Indexer.Function;

public class BucketCrawler
{
    private readonly IManifestCrawler _manifestCrawler;
    private readonly IIndexer _indexer;

    public BucketCrawler(IManifestCrawler manifestCrawler, IIndexer indexer)
    {
        _manifestCrawler = manifestCrawler;
        _indexer = indexer;
    }

    [FunctionName("BucketCrawler")]
    public async Task Run(
        [QueueTrigger(Constants.BucketsQueue)] QueueItem queueItem,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Clone/Update bucket repository and retrieve manifests
        logger.LogInformation($"Generating manifests list for '{queueItem.Bucket}'");
        var manifestsFromBucket = _manifestCrawler
            .GetManifestsFromRepository(queueItem.Bucket, cancellationToken)
            .ToArray();

        logger.LogInformation($"Found {manifestsFromBucket.Length} manifests for {queueItem.Bucket}");

        foreach (var manifestInfo in manifestsFromBucket)
        {
            manifestInfo.Metadata.SetRepositoryMetadata(queueItem.Official, queueItem.Stars);
        }

        // Retrieve all manifests for this repository from the index
        var manifestsFromIndex = (await _indexer.GetExistingManifestsAsync(queueItem.Bucket, cancellationToken)).ToArray();

        // Compute changes
        var manifestsToRemove = manifestsFromIndex.Except(manifestsFromBucket, ManifestComparer.ManifestIdComparer).ToArray();
        var manifestsToAdd = manifestsFromBucket.Except(manifestsFromIndex, ManifestComparer.ManifestIdComparer).ToArray();
        var manifestsToUpdate = manifestsFromBucket.Except(manifestsToAdd).Except(manifestsFromIndex, ManifestComparer.ManifestExactComparer).ToArray();
        logger.LogInformation($"{manifestsFromIndex.Length} existing manifests. {manifestsToAdd.Length} manifests to add / {manifestsToRemove.Length} manifests to remove / {manifestsToUpdate.Length} manifests to update");

        // Remove entries
        if (manifestsToRemove.Any())
        {
            await _indexer.DeleteManifestsAsync(manifestsToRemove, cancellationToken);
        }

        // Add / Update entries
        var manifests = manifestsToAdd.Concat(manifestsToUpdate).ToArray();
        if (manifests.Any())
        {
            await _indexer.AddManifestsAsync(manifests, cancellationToken);
        }
    }
}
