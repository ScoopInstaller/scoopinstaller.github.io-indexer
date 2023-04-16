using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using ScoopSearch.Functions.Data;
using ScoopSearch.Functions.Indexer;
using ScoopSearch.Functions.Manifest;

namespace ScoopSearch.Functions.Function.Indexing;

public class BucketCrawlerActivity
{
    private readonly IManifestCrawler _manifestCrawler;
    private readonly IIndexer _indexer;

    public BucketCrawlerActivity(IManifestCrawler manifestCrawler, IIndexer indexer)
    {
        _manifestCrawler = manifestCrawler;
        _indexer = indexer;
    }

    [FunctionName(nameof(CrawlBucket))]
    public async Task CrawlBucket(
        [ActivityTrigger] IDurableActivityContext context,
        ILogger logger)
    {
        var cancellationToken = CancellationToken.None;
        var bucketInfo = context.GetInput<BucketInfo>();

        // Clone/Update bucket repository and retrieve manifests
        logger.LogInformation($"Generating manifests list for '{bucketInfo.Uri}'");

        var manifestsFromBucket = _manifestCrawler
            .GetManifestsFromRepository(bucketInfo.Uri, cancellationToken)
            .ToArray();

        logger.LogInformation($"Found {manifestsFromBucket.Length} manifests for {bucketInfo.Uri}");

        foreach (var manifestInfo in manifestsFromBucket)
        {
            manifestInfo.Metadata.SetRepositoryMetadata(bucketInfo.Official, bucketInfo.Stars);
        }

        // Retrieve all manifests for this repository from the index
        var manifestsFromIndex = (await _indexer.GetExistingManifestsAsync(bucketInfo.Uri, cancellationToken)).ToArray();

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
