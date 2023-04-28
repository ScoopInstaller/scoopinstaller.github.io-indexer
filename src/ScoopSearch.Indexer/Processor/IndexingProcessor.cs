using Microsoft.Extensions.Logging;
using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.Extensions;
using ScoopSearch.Indexer.Indexer;
using ScoopSearch.Indexer.Manifest;

namespace ScoopSearch.Indexer.Processor;

internal class IndexingProcessor : IIndexingProcessor
{
    private readonly ISearchClient _searchClient;
    private readonly ISearchIndex _azureSearchIndex;
    private readonly ILogger _logger;

    public IndexingProcessor(ISearchClient searchClient, ISearchIndex azureSearchIndex, ILogger<IndexingProcessor> logger)
    {
        _searchClient = searchClient;
        _azureSearchIndex = azureSearchIndex;
        _logger = logger;
    }

    public async Task CreateIndexIfRequiredAsync(CancellationToken cancellationToken)
    {
        await _azureSearchIndex.CreateIndexIfRequiredAsync(cancellationToken);
    }

    public async Task CleanIndexFromNonExistentBucketsAsync(Uri[] buckets, CancellationToken cancellationToken)
    {
        var allBucketsFromIndex = await _searchClient.GetBucketsAsync(cancellationToken);
        var deletedBuckets = allBucketsFromIndex.Except(buckets).ToArray();
        _logger.LogInformation($"{deletedBuckets.Length} buckets to remove from the index.");

        var manifestsToRemove = (await _searchClient.GetExistingManifestsAsync(deletedBuckets, cancellationToken)).ToArray();
        _logger.LogInformation($"{manifestsToRemove.Length} manifests to remove from the index.");

        if (manifestsToRemove.Any())
        {
            await _searchClient.DeleteManifestsAsync(manifestsToRemove, cancellationToken);
        }
    }

    public async Task UpdateIndexWithManifestsAsync(ManifestInfo[] manifestsFromRepositories, CancellationToken cancellationToken)
    {
        var manifestsFromIndex = (await _searchClient.GetAllManifestsAsync(cancellationToken)).ToArray();

        // Compute changes
        var manifestsToRemove = manifestsFromIndex.Except(manifestsFromRepositories, ManifestComparer.ManifestIdComparer).ToArray();
        var manifestsToAdd = manifestsFromRepositories.Except(manifestsFromIndex, ManifestComparer.ManifestIdComparer).ToArray();
        var manifestsToUpdate = manifestsFromRepositories.Except(manifestsToAdd).Except(manifestsFromIndex, ManifestComparer.ManifestExactComparer).ToArray();
        _logger.LogInformation($"{manifestsFromIndex.Length} existing manifests in the index. {manifestsToAdd.Length} manifests to add / {manifestsToRemove.Length} manifests to remove / {manifestsToUpdate.Length} manifests to update");

        manifestsToRemove
            .GroupBy(_ => _.Metadata.Repository)
            .ToArray()
            .ForEach(_ => _logger.LogInformation($"Removing {_.Count()} manifests from {_.Key}"));

        manifestsToAdd
            .GroupBy(_ => _.Metadata.Repository)
            .ToArray()
            .ForEach(_ => _logger.LogInformation($"Adding {_.Count()} manifests from {_.Key}"));

        manifestsToUpdate
            .GroupBy(_ => _.Metadata.Repository)
            .ToArray()
            .ForEach(_ => _logger.LogInformation($"Updating {_.Count()} manifests from {_.Key}"));

        if (manifestsToRemove.Any())
        {
            await _searchClient.DeleteManifestsAsync(manifestsToRemove, cancellationToken);
        }

        var manifestsToAddOrUpdate = manifestsToAdd.Concat(manifestsToUpdate).ToArray();
        if (manifestsToAddOrUpdate.Any())
        {
            await _searchClient.UpsertManifestsAsync(manifestsToAddOrUpdate, cancellationToken);
        }
    }
}
