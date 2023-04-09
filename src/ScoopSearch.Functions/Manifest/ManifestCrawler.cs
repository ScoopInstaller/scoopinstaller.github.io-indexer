using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using ScoopSearch.Functions.Data;
using ScoopSearch.Functions.Git;

namespace ScoopSearch.Functions.Manifest
{
    internal class ManifestCrawler : IManifestCrawler
    {
        private readonly IGitRepositoryProvider _gitRepositoryProvider;
        private readonly IKeyGenerator _keyGenerator;
        private readonly ILogger<ManifestCrawler> _logger;

        public ManifestCrawler(IGitRepositoryProvider gitRepositoryProvider, IKeyGenerator keyGenerator, ILogger<ManifestCrawler> logger)
        {
            _gitRepositoryProvider = gitRepositoryProvider;
            _keyGenerator = keyGenerator;
            _logger = logger;
        }

        public IEnumerable<ManifestInfo> GetManifestsFromRepository(Uri bucketUri, CancellationToken cancellationToken)
        {
            var results = new List<ManifestInfo>();

            var repository = _gitRepositoryProvider.Download(bucketUri, cancellationToken);
            if (repository != null)
            {
                _logger.LogDebug("Generating manifest infos from repository '{Repository}'", bucketUri);

                var entries = repository.GetEntriesFromIndex().ToArray();
                var manifestsSubPath = entries.Any(_ => _ is { Type: EntryType.Directory, Path: "bucket" })
                    ? "bucket"
                    : string.Empty;

                bool IsManifestPredicate(string filePath) => Path.GetDirectoryName(filePath)?.StartsWith(manifestsSubPath) == true
                                                             && ".json".Equals(Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase);

                var commitCache = repository.GetCommitsCache(IsManifestPredicate, cancellationToken);

                foreach (var entry in entries
                             .Where(x => x.Type == EntryType.File && IsManifestPredicate(x.Path))
                             .TakeWhile(x => !cancellationToken.IsCancellationRequested))
                {
                    if (commitCache.TryGetValue(entry.Path, out var commits) && commits.FirstOrDefault() is { } commit)
                    {
                        var manifestMetadata = new ManifestMetadata(
                            bucketUri.AbsoluteUri,
                            repository.GetBranchName(),
                            entry.Path,
                            commit.AuthorName,
                            commit.AuthorEmail,
                            commit.Date,
                            commit.Sha);

                        var manifestData = repository.ReadContent(entry);
                        var manifest = CreateManifest(manifestData, manifestMetadata);
                        if (manifest != null)
                        {
                            results.Add(manifest);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Unable to find a commit for manifest '{Manifest}' from '{Repository}'", entry.Path, bucketUri);
                    }
                }

                repository.Delete();
            }

            return results;
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
}
