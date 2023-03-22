using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ScoopSearch.Functions.Git
{
    internal class GitRepository : IGitRepository
    {
        private static readonly Signature _signature = new Signature("Merge", "merge@example.com", DateTimeOffset.Now);

        private readonly ExecutionContextOptions _executionContextOptions;
        private readonly ILogger<GitRepository> _logger;

        public GitRepository(IOptions<ExecutionContextOptions> executionContextOptions, ILogger<GitRepository> logger)
        {
            _executionContextOptions = executionContextOptions.Value;
            _logger = logger;
        }

        public string DownloadRepository(Uri uri, CancellationToken cancellationToken)
        {
            var repositoryRoot = Path.Combine(RepositoriesRoot, uri.AbsolutePath.Substring(1)); // Remove leading slash
            try
            {
                if (Directory.Exists(repositoryRoot))
                {
                    try
                    {
                        PullRepository(repositoryRoot, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Unable to pull repository '{uri}' to '{repositoryRoot}'.");
                        DeleteRepository(repositoryRoot);
                        CloneRepository(uri, repositoryRoot, cancellationToken);
                    }
                }
                else
                {
                    CloneRepository(uri, repositoryRoot, cancellationToken);
                }

                return repositoryRoot;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Unable to clone repository '{uri}' to '{repositoryRoot}'.");
                DeleteRepository(repositoryRoot);
                return null;
            }
        }

        public void DeleteRepository(string repository)
        {
            var directory = new DirectoryInfo(repository);
            if (directory.Exists)
            {
                directory.Attributes = FileAttributes.Normal;

                foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    info.Attributes = FileAttributes.Normal;
                }

                directory.Delete(true);
            }
        }

        public IDictionary<string, Commit[]> GetCommitsCache(Repository repository, Predicate<string> filter, CancellationToken cancellationToken)
        {
            var commitsCache = new ConcurrentDictionary<string, ImmutableList<Commit>>();
            
            Parallel.ForEach(
                repository.Head.Commits,
                new ParallelOptions { CancellationToken = cancellationToken },
                commit =>
                {
                    IEnumerable<string> filesInCommit = null;
                    if (commit.Parents.Any())
                    {
                        var treeDiff = repository.Diff.Compare<TreeChanges>(commit.Parents.First().Tree, commit.Tree);
                        filesInCommit = treeDiff.Select(x => x.Path);
                    }
                    else
                    {
                        var trees = new[] {commit.Tree}.Concat(commit.Tree.Select(x => x.Target).OfType<Tree>());
                        filesInCommit = trees.SelectMany(x => x.Where(y => y.Mode != Mode.Directory).Select(y => y.Path));
                    }

                    foreach (var filePath in filesInCommit.Where(x => filter(x)))
                    {
                        commitsCache.AddOrUpdate(
                            filePath,
                            ImmutableList.Create(commit),
                            (key, list) => list.Add(commit));
                    }
                });

            return commitsCache.ToDictionary(k => k.Key, v => v.Value.ToArray());
        }

        private string RepositoriesRoot
        {
            get
            {
                var root = Environment.GetEnvironmentVariable("TEMP") ?? _executionContextOptions.AppDirectory;
                return Path.Combine(root, "repositories");
            }
        }

        private void PullRepository(string repositoryRoot, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Pulling repository '{repositoryRoot}'");

            using (var repository = new Repository(repositoryRoot))
            {
                if (repository.Branches.Any())
                {
                    var remote = repository.Network.Remotes.Single();

                    var fetchOptions = CreateOptions<FetchOptions>(cancellationToken);

                    var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                    Commands.Fetch(repository, remote.Name, refSpecs, fetchOptions, null);
                    var result = repository.MergeFetchedRefs(
                        _signature,
                        new MergeOptions {FastForwardStrategy = FastForwardStrategy.FastForwardOnly});

                    if (result.Status == MergeStatus.Conflicts)
                    {
                        throw new InvalidOperationException($"Merge conflict in repository {repositoryRoot}");
                    }
                }
                else
                {
                    _logger.LogWarning($"No remote branch found for repository {repositoryRoot}");
                }
            }
        }

        private void CloneRepository(Uri uri, string repositoryRoot, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Cloning repository '{uri}' in '{repositoryRoot}'");

            var cloneOptions = CreateOptions<CloneOptions>(cancellationToken);
            cloneOptions.RecurseSubmodules = false;
            Repository.Clone(uri.AbsoluteUri, repositoryRoot, cloneOptions);

            using (var repository = new Repository(repositoryRoot))
            {
                if (!repository.Branches.Any())
                {
                    _logger.LogWarning($"No remote branch found for repository {repositoryRoot}");
                }
            }
        }

        private T CreateOptions<T>(CancellationToken cancellationToken)
            where T : FetchOptionsBase, new()
        {
            var options = new T();
            options.OnProgress = x => !cancellationToken.IsCancellationRequested;
            options.OnTransferProgress = x => !cancellationToken.IsCancellationRequested;

            return options;
        }
    }
}
