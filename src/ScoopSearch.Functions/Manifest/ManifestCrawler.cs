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
        private readonly IGitRepository _gitRepository;
        private readonly IKeyGenerator _keyGenerator;
        private readonly ILogger<ManifestCrawler> _logger;

        public ManifestCrawler(IGitRepository gitRepository, IKeyGenerator keyGenerator, ILogger<ManifestCrawler> logger)
        {
            _gitRepository = gitRepository;
            _keyGenerator = keyGenerator;
            _logger = logger;
        }

        public IEnumerable<ManifestInfo> GetManifestsFromRepository(Uri bucketUri, CancellationToken cancellationToken)
        {
            var results = new List<ManifestInfo>();

            var repositoryRoot = _gitRepository.DownloadRepository(bucketUri, cancellationToken);
            if (repositoryRoot != null)
            {
                using (var repository = new Repository(repositoryRoot))
                {
                    var repositoryRemote = repository.Network.Remotes.Single().Url;
                    var branchName = repository.Head.FriendlyName;
                    if (repository.Head.Tip == null)
                    {
                        _logger.LogInformation($"{bucketUri} is an empty repository");
                        return Enumerable.Empty<ManifestInfo>();
                    }

                    var manifestsSubPath = repository.Head.Tip["bucket"]?.Mode == Mode.Directory
                        ? "bucket"
                        : string.Empty;
                    var manifests = repository.Index.Where(x => IsManifestFile(x.Path, manifestsSubPath));
                    bool IsManifestPredicate(string filePath) => IsManifestFile(filePath, manifestsSubPath);
                    var commitCache = _gitRepository.GetCommitsCache(repository, IsManifestPredicate, cancellationToken);

                    foreach (var entry in manifests.TakeWhile(x => !cancellationToken.IsCancellationRequested))
                    {
                        var commit = commitCache[entry.Path];

                        var manifestMetadata = new ManifestMetadata(
                            repositoryRemote,
                            branchName,
                            entry.Path,
                            commit.AuthorName,
                            commit.AuthorEmail,
                            commit.Date,
                            commit.Sha);

                        var manifestData = File.ReadAllText(Path.Combine(repository.Info.WorkingDirectory, entry.Path));
                        var manifest = CreateManifest(manifestData, manifestMetadata);
                        if (manifest != null)
                        {
                            results.Add(manifest);
                        }
                    }
                }

                _gitRepository.DeleteRepository(repositoryRoot);
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
