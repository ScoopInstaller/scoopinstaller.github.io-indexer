using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
                var manifestsSubPath = repository.GetEntriesFromIndex(x => x == "bucket").Any(x => x.Mode == Mode.Directory)
                    ? "bucket"
                    : string.Empty;
                var manifests = repository.GetEntriesFromIndex(x => IsManifestFile(x, manifestsSubPath));
                bool IsManifestPredicate(string filePath) => IsManifestFile(filePath, manifestsSubPath);
                var commitCache = repository.GetCommitsCache(IsManifestPredicate, cancellationToken);

                foreach (var entry in manifests.TakeWhile(x => !cancellationToken.IsCancellationRequested))
                {
                    var commit = commitCache[entry.Path].First();

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

                repository.Delete();
            }

            return results;
        }

        private bool IsManifestFile(string filePath, string manifestsSubPath)
        {
            return Path.GetDirectoryName(filePath)?.StartsWith(manifestsSubPath) == true
                   && ".json".Equals(Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase);
        }

        private ManifestInfo? CreateManifest(string contentJson, ManifestMetadata metadata)
        {
            try
            {
                return JsonConvert.DeserializeObject<ManifestInfo>(
                    contentJson,
                    new JsonSerializerSettings
                    {
                        Context = new StreamingContext(
                            StreamingContextStates.Other,
                            (_keyGenerator.Generate(metadata), metadata))
                    });
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, $"Unable to parse manifest {metadata.FilePath} in {metadata.Repository}.");
            }

            return null;
        }
    }
}
