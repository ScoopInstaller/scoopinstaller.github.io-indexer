using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using ScoopSearch.Indexer.Buckets;
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

    public IAsyncEnumerable<ManifestInfo> FetchManifestsAsync(Bucket bucket, CancellationToken cancellationToken)
    {
        // Clone/Update bucket repository and retrieve manifests
        _logger.LogDebug("Generating manifests list for {Bucket}", bucket.Uri);

        return GetManifestsFromRepositoryAsync(bucket.Uri, cancellationToken);
    }

    private async IAsyncEnumerable<ManifestInfo> GetManifestsFromRepositoryAsync(Uri bucketUri, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var repository = _gitRepositoryProvider.Download(bucketUri, cancellationToken);
        if (repository == null)
        {
            yield break;
        }

        _logger.LogDebug("Generating manifest infos from repository {Repository}", bucketUri);

        var files = repository.GetFilesFromIndex().ToArray();
        var manifestsSubPath = files.Any(_ => _.StartsWith("bucket/")) ? "bucket" : null;

        var commitCache = await repository.GetCommitsCacheAsync(_ => IsManifestPredicate(manifestsSubPath, _), cancellationToken);

        foreach (var filePath in files
                     .Where(_ => IsManifestPredicate(manifestsSubPath, _))
                     .TakeWhile(_ => !cancellationToken.IsCancellationRequested))
        {
            if (commitCache.TryGetValue(filePath, out var commits) && commits.FirstOrDefault() is { } commit)
            {
                var manifestData = await repository.ReadContentAsync(filePath, cancellationToken);
                var manifestMetadata = new ManifestMetadata(
                    bucketUri.AbsoluteUri,
                    repository.GetBranchName(),
                    filePath,
                    commit.Date,
                    commit.Sha);

                var manifest = CreateManifest(manifestData, manifestMetadata);
                if (manifest != null)
                {
                    yield return manifest;
                }
            }
            else
            {
                _logger.LogWarning("Unable to find a commit for manifest {Manifest} from {Repository}", filePath, bucketUri);
            }
        }

        repository.Delete();
    }

    private static bool IsManifestPredicate(string? manifestsSubPath, string filePath)
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
            _logger.LogError(ex, "Unable to parse manifest {Manifest} from {Repository}", metadata.FilePath, metadata.Repository);
        }

        return null;
    }
}
