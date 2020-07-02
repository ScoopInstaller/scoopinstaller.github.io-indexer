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

        public IEnumerable<ManifestInfo> GetManifestsFromRepository(Uri url, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Generating manifests list for '{url}'");

            var repositoryRoot = _gitRepository.GetRepository(url, cancellationToken);
            if (repositoryRoot != null)
            {
                using (var repository = new Repository(repositoryRoot))
                {
                    var repositoryRemote = repository.Network.Remotes.Single().Url;
                    var branchName = repository.Head.FriendlyName;

                    var manifestsSubPath = repository.Index.Any(x => x.Path.StartsWith("bucket/"))
                        ? "bucket/"
                        : string.Empty;
                    var manifests = repository.Index.Where(x => IsManifestFile(x.Path, manifestsSubPath));
                    var commitCache = _gitRepository.GetCommitsCache(repository,
                        filePath => IsManifestFile(filePath, manifestsSubPath));

                    foreach (var entry in manifests.TakeWhile(x => !cancellationToken.IsCancellationRequested))
                    {
                        var commit = commitCache[entry.Path].First();


                        var manifestMetadata = new ManifestMetadata(
                            repositoryRemote,
                            branchName,
                            entry.Path,
                            commit.Author.Name,
                            commit.Author.Email,
                            commit.Author.When,
                            commit.Sha);

                        var blob = (Blob)commit[entry.Path].Target;
                        var manifest = CreateManifest(blob.GetContentText(), manifestMetadata);
                        if (manifest != null)
                        {
                            yield return manifest;
                        }
                    }
                }
            }
        }

        private bool IsManifestFile(string filePath, string manifestsSubPath)
        {
            return filePath.StartsWith(manifestsSubPath)
                   && ".json".Equals(Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase);
        }

        private ManifestInfo CreateManifest(string contentJson, ManifestMetadata metadata)
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
                _logger.LogWarning(ex, $"Unable to read manifest in {metadata.FilePath} ({metadata.Repository}). Error is: {ex.Message}");
            }

            return null;
        }
    }
}
