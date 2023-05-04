using System.Text.Json.Serialization;
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

        await DeleteManifestsFromDeletedBucketsAsync(manifestsFromIndex, manifestsFromRepositories, cancellationToken);
        (manifestsFromRepositories, var duplicatedManifests) = UpdateManifestsMetadataWithDuplicateInfo(manifestsFromRepositories);
        var upsertedManifests = await UpsertManifestsAsync(manifestsFromIndex, manifestsFromRepositories, cancellationToken);
        var manifestsToPatch = duplicatedManifests.Except(upsertedManifests, ManifestComparer.ManifestIdComparer).ToArray();
        await PatchManifestsAsync(manifestsToPatch, cancellationToken);
    }

    private async Task DeleteManifestsFromDeletedBucketsAsync(ManifestInfo[] manifestsFromIndex, ManifestInfo[] manifestsFromRepositories, CancellationToken cancellationToken)
    {
        var manifestsToRemove = manifestsFromIndex.Except(manifestsFromRepositories, ManifestComparer.ManifestIdComparer).ToArray();
        _logger.LogInformation("{Count} manifests to delete from the index (not found in the existing buckets anymore)", manifestsToRemove.Length);
        if (manifestsToRemove.Any())
        {
            manifestsToRemove
                .GroupBy(_ => _.Metadata.Repository)
                .ForEach(_ => _logger.LogInformation("Deleting {Count} manifests from {Bucket}", _.Count(), _.Key));
            await _searchClient.DeleteManifestsAsync(manifestsToRemove, cancellationToken);
        }
    }

    private (ManifestInfo[] manifestsFromRepositories, ManifestInfo[] duplicatedManifests) UpdateManifestsMetadataWithDuplicateInfo(ManifestInfo[] manifestsFromRepositories)
    {
        var duplicatedManifestsGroups = manifestsFromRepositories
            .GroupBy(_ => _.Metadata.ManifestHash)
            .Where(_ => _.Count() > 1) // Limit to duplicates
            .Where(_ => _.Select(_ => _.Metadata.Repository).Distinct().Count() > 1) // Limit to duplicates when found in different repositories
            .ToArray();

        foreach (var duplicatedManifestsGroup in duplicatedManifestsGroups)
        {
            var prioritizedManifests = duplicatedManifestsGroup
                .OrderByDescending(_ => _.Metadata.OfficialRepositoryNumber)
                .ThenBy(_ => _.Metadata.Committed)
                .ToArray();
            var originalManifest = prioritizedManifests.First();

            _logger.LogInformation("Duplicated manifests with hash '{Hash}' found in {Manifests}. Choosing {Manifest} as the original one",
                duplicatedManifestsGroup.Key,
                string.Join(", ", duplicatedManifestsGroup.Select(_ => _.Metadata.Repository + "/" + _.Metadata.FilePath)),
                originalManifest.Metadata.Repository + "/" + originalManifest.Metadata.FilePath);

            prioritizedManifests.Skip(1).ForEach(_ => _.Metadata.SetDuplicateOf(originalManifest.Id));
        }

        return (manifestsFromRepositories, duplicatedManifestsGroups.SelectMany(_ => _).ToArray());
    }

    private async Task<ManifestInfo[]> UpsertManifestsAsync(ManifestInfo[] manifestsFromIndex, ManifestInfo[] manifestsFromRepositories, CancellationToken cancellationToken)
    {
        var manifestsToAdd = manifestsFromRepositories.Except(manifestsFromIndex, ManifestComparer.ManifestIdComparer).ToArray();
        var manifestsToUpdate = manifestsFromRepositories.Except(manifestsToAdd).Except(manifestsFromIndex, ManifestComparer.ManifestExactComparer).ToArray();
        _logger.LogInformation("{Count} manifests to add to the index", manifestsToAdd.Length);
        _logger.LogInformation("{Count} manifests to update in the index", manifestsToUpdate.Length);

        var manifestsToAddOrUpdate = manifestsToAdd.Concat(manifestsToUpdate).ToArray();
        if (manifestsToAddOrUpdate.Any())
        {
            manifestsToAdd
                .GroupBy(_ => _.Metadata.Repository)
                .ToArray()
                .ForEach(_ => _logger.LogInformation("Adding {Count} manifests from {Bucket}", _.Count(), _.Key));

            manifestsToUpdate
                .GroupBy(_ => _.Metadata.Repository)
                .ToArray()
                .ForEach(_ => _logger.LogInformation("Updating {Count} manifests from {Bucket}", _.Count(), _.Key));
            await _searchClient.UpsertManifestsAsync(manifestsToAddOrUpdate, cancellationToken);
        }

        return manifestsToAddOrUpdate;
    }

    private async Task PatchManifestsAsync(ManifestInfo[] manifestsToPatch, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Count} manifests to patch in the index", manifestsToPatch.Length);
        if (manifestsToPatch.Any())
        {
            manifestsToPatch
                .GroupBy(_ => _.Metadata.Repository)
                .ToArray()
                .ForEach(_ => _logger.LogInformation("Patching {Count} manifests from {Bucket}", _.Count(), _.Key));

            var patches = manifestsToPatch.Select(_ => new ManifestInfoPatch(_.Id, _.Metadata.DuplicateOf));
            await _searchClient.PatchAsync(patches, cancellationToken);
        }
    }

    private class ManifestInfoPatch
    {
        public ManifestInfoPatch(string id, string? duplicatedFrom)
        {
            Id = id;
            Metadata = new ManifestMetadataPatch(duplicatedFrom);
        }

        [JsonInclude]
        public string Id { get; }

        [JsonInclude]
        public ManifestMetadataPatch Metadata { get; }

        public class ManifestMetadataPatch
        {
            public ManifestMetadataPatch(string? duplicateOf)
            {
                DuplicateOf = duplicateOf;
            }

            [JsonInclude]
            public string? DuplicateOf { get; }
        }
    }
}
