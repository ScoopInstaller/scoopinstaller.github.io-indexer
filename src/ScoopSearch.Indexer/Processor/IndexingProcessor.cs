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
        var manifestsToRemove = await _searchClient
            .GetExistingManifestsAsync(deletedBuckets, cancellationToken)
            .ToArrayAsync(cancellationToken: cancellationToken);
        _logger.LogInformation("{ManifestsCount} manifests to delete from the index (associated to {BucketsCount} non existing buckets)", manifestsToRemove.Length, deletedBuckets.Length);

        if (manifestsToRemove.Any())
        {
            await _searchClient.DeleteManifestsAsync(manifestsToRemove, cancellationToken);
        }
    }

    public async Task UpdateIndexWithManifestsAsync(ManifestInfo[] manifestsFromRepositories, CancellationToken cancellationToken)
    {
        var manifestsFromIndex = await _searchClient.GetAllManifestsAsync(cancellationToken).ToArrayAsync(cancellationToken);
        _logger.LogInformation("{Count} manifests found in the index", manifestsFromIndex.Length);

        var manifestsToDelete = manifestsFromIndex.Except(manifestsFromRepositories, ManifestComparer.ManifestIdComparer).ToArray();
        await DeleteManifestsFromIndexAsync(manifestsToDelete, cancellationToken);

        UpdateManifestsMetadataWithDuplicateInfo(ref manifestsFromRepositories);
        var manifestsToAdd = manifestsFromRepositories.Except(manifestsFromIndex, ManifestComparer.ManifestIdComparer).ToArray();
        var manifestsToUpdate = manifestsFromRepositories.Except(manifestsToAdd).Except(manifestsFromIndex, ManifestComparer.ManifestExactComparer).ToArray();
        await UpsertManifestsAsync(manifestsToAdd, manifestsToUpdate, cancellationToken);
    }

    private async Task DeleteManifestsFromIndexAsync(ManifestInfo[] manifestsToDelete, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Count} manifests to delete from the index (not found in the existing buckets anymore)", manifestsToDelete.Length);
        if (manifestsToDelete.Any())
        {
            manifestsToDelete
                .GroupBy(_ => _.Metadata.Repository)
                .ForEach(_ => _logger.LogInformation("Deleting {Count} manifests from {Bucket}", _.Count(), _.Key));
            await _searchClient.DeleteManifestsAsync(manifestsToDelete, cancellationToken);
        }
    }

    private void UpdateManifestsMetadataWithDuplicateInfo(ref ManifestInfo[] manifestsFromRepositories)
    {
        var duplicatedManifestsGroups = manifestsFromRepositories
            .Select(_ => (manifest: _, hash: string.Concat(_.Name!.Trim().ToLowerInvariant(), _.Version?.Trim().ToLowerInvariant()).Sha1Sum()))
            .GroupBy(_ => _.hash)
            .Where(_ => _.Count() > 1) // Limit to duplicates
            .Where(_ => _.Select(_ => _.manifest.Metadata.Repository).Distinct().Count() > 1) // Limit to duplicates when found in different repositories
            .ToArray();

        foreach (var duplicatedManifestsGroup in duplicatedManifestsGroups)
        {
            var prioritizedManifests = duplicatedManifestsGroup
                .Select(_ => _.manifest)
                .OrderByDescending(_ => _.Metadata.OfficialRepositoryNumber)
                .ThenBy(_ => _.Metadata.Committed)
                .ThenByDescending(_ => _.Metadata.RepositoryStars)
                .ThenBy(_ => _.Metadata.Repository)
                .ToArray();
            var originalManifest = prioritizedManifests.First();

            _logger.LogDebug("Duplicated manifests with hash {Hash} found in {Manifests}. Choosing {Manifest} as the original one",
                duplicatedManifestsGroup.Key,
                string.Join(", ", duplicatedManifestsGroup.Select(_ => _.manifest.Metadata.Repository + "/" + _.manifest.Metadata.FilePath)),
                originalManifest.Metadata.Repository + "/" + originalManifest.Metadata.FilePath);

            prioritizedManifests.Skip(1)
                .Where(_ => _.Metadata.OfficialRepositoryNumber != 1) // Never mark an official manifest as a duplicate
                .ForEach(_ => _.Metadata.SetDuplicateOf(originalManifest.Id));
        }

        manifestsFromRepositories
            .GroupBy(_ => _.Metadata.Repository)
            .Select(_ => (bucket: _.Key, duplicates: _.Count(_ => _.Metadata.DuplicateOf != null)))
            .Where(_ => _.duplicates > 0)
            .ForEach(_ => _logger.LogInformation("{Count} duplicated manifests found in {Bucket}", _.duplicates, _.bucket));
    }

    private async Task UpsertManifestsAsync(ManifestInfo[] manifestsToAdd, ManifestInfo[] manifestsToUpdate, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Count} manifests to add to the index", manifestsToAdd.Length);
        _logger.LogInformation("{Count} manifests to update in the index", manifestsToUpdate.Length);

        var manifestsToAddOrUpdate = manifestsToAdd.Concat(manifestsToUpdate).ToArray();
        if (manifestsToAddOrUpdate.Any())
        {
            manifestsToAdd
                .GroupBy(_ => _.Metadata.Repository)
                .ForEach(_ => _logger.LogInformation("Adding {Count} manifests from {Bucket}", _.Count(), _.Key));

            manifestsToUpdate
                .GroupBy(_ => _.Metadata.Repository)
                .ForEach(_ => _logger.LogInformation("Updating {Count} manifests from {Bucket}", _.Count(), _.Key));
            await _searchClient.UpsertManifestsAsync(manifestsToAddOrUpdate, cancellationToken);
        }
    }
}
