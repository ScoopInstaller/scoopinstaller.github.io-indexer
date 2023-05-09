using Microsoft.Extensions.Logging;
using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.Git;
using ScoopSearch.Indexer.Manifest;

namespace ScoopSearch.Indexer.Processor;

internal class FetchManifestsProcessor : IFetchManifestsProcessor
{
    private readonly IGitRepositoryProvider _gitRepositoryProvider;
    private readonly IKeyGenerator _keyGenerator;
    private readonly ILogger _logger;

    public FetchManifestsProcessor(IGitRepositoryProvider gitRepositoryProvider, IKeyGenerator keyGenerator, ILogger<FetchManifestsProcessor> logger)
    {
        _gitRepositoryProvider = gitRepositoryProvider;
        _keyGenerator = keyGenerator;
        _logger = logger;
    }

    public async Task<ManifestInfo[]> FetchManifestsAsync(BucketInfo bucketInfo, CancellationToken cancellationToken)
    {
        // Clone/Update bucket repository and retrieve manifests
        _logger.LogInformation("Generating manifests list for '{Bucket}'", bucketInfo.Uri);

        var manifestsFromBucket = this
            .GetManifestsFromRepository(bucketInfo.Uri, cancellationToken)
            .ToArray();

        _logger.LogInformation("Found {Count} manifests for {Bucket}", manifestsFromBucket.Length, bucketInfo.Uri);

        foreach (var manifestInfo in manifestsFromBucket)
        {
            manifestInfo.Metadata.SetRepositoryMetadata(bucketInfo.Official, bucketInfo.Stars);
        }

        return await Task.FromResult(manifestsFromBucket);
    }

    private IEnumerable<ManifestInfo> GetManifestsFromRepository(Uri bucketUri, CancellationToken cancellationToken)
    {
        var results = new List<ManifestInfo>();

        var repository = _gitRepositoryProvider.Download(bucketUri, cancellationToken);
        if (repository == null)
        {
            return results;
        }

        _logger.LogDebug("Generating manifest infos from repository '{Repository}'", bucketUri);

        var files = repository.GetFilesFromIndex().ToArray();
        var manifestsSubPath = files.Any(_ => _.StartsWith("bucket/")) ? "bucket" : null;

        var commitCache = repository.GetCommitsCache(_ => IsManifestPredicate(manifestsSubPath, _), cancellationToken);

        foreach (var filePath in files
                     .Where(_ => IsManifestPredicate(manifestsSubPath, _))
                     .TakeWhile(_ => !cancellationToken.IsCancellationRequested))
        {
            if (commitCache.TryGetValue(filePath, out var commits) && commits.FirstOrDefault() is { } commit)
            {
                var manifestData = repository.ReadContent(filePath);
                var manifestMetadata = new ManifestMetadata(
                    bucketUri.AbsoluteUri,
                    repository.GetBranchName(),
                    filePath,
                    commit.Date,
                    commit.Sha);

                var manifest = CreateManifest(manifestData, manifestMetadata);
                if (manifest != null)
                {
                    results.Add(manifest);
                }
            }
            else
            {
                _logger.LogWarning("Unable to find a commit for manifest '{Manifest}' from '{Repository}'", filePath, bucketUri);
            }
        }

        repository.Delete();

        return results;
    }

    bool IsManifestPredicate(string? manifestsSubPath, string filePath)
    {
        var isManifest = manifestsSubPath == null ? Path.GetDirectoryName(filePath)?.Length == 0 : Path.GetDirectoryName(filePath)?.StartsWith(manifestsSubPath) == true;
        isManifest &= Path.GetFileName(filePath)[0] != '.';
        isManifest &= ".json".Equals(Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase);

        return isManifest;
    }

    private ManifestInfo? CreateManifest(string contentJson, ManifestMetadata metadata)
    {
        try
        {
            return ManifestInfo.Deserialize(contentJson, _keyGenerator.Generate(metadata), metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to parse manifest '{Manifest}' from '{Repository}'", metadata.FilePath, metadata.Repository);
        }

        return null;
    }
}
