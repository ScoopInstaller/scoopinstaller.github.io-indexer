using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using ScoopSearch.Functions.Data;

namespace ScoopSearch.Functions.Git
{
    internal class GitRepository : IGitRepository
    {
        private static readonly Signature _signature = new Signature("Merge", "merge@example.com", DateTimeOffset.Now);

        private readonly ILogger<GitRepository> _logger;
        private readonly string _repositoriesRoot;
        private readonly string _gitExecutable;

        public GitRepository(ILogger<GitRepository> logger)
            : this(logger, Path.Combine(Path.GetTempPath(), "repositories"))
        {
        }

        internal GitRepository(ILogger<GitRepository> logger, string repositoriesRoot)
        {
            _logger = logger;
            _repositoriesRoot = repositoriesRoot;
            _gitExecutable = GetGitExecutable();
        }

        public string? DownloadRepository(Uri uri, CancellationToken cancellationToken)
        {
            var repositoryRoot = Path.Combine(_repositoriesRoot, uri.AbsolutePath.Substring(1)); // Remove leading slash
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
                        _logger.LogWarning(ex, $"Unable to pull repository '{uri}' to '{repositoryRoot}'");
                        DeleteRepository(repositoryRoot);
                        CloneRepository(uri, repositoryRoot, cancellationToken);
                    }
                }
                else
                {
                    CloneRepository(uri, repositoryRoot, cancellationToken);
                }

                using (var repository = new Repository(repositoryRoot))
                {
                    if (!repository.Branches.Any())
                    {
                        _logger.LogError($"No remote branch found for repository '{repositoryRoot}'");
                        return null;
                    }
                }

                return repositoryRoot;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to clone repository '{uri}' to '{repositoryRoot}'");
                DeleteRepository(repositoryRoot);
                return null;
            }
        }

        public void DeleteRepository(string repository)
        {
            var directory = new DirectoryInfo(repository);
            if (directory.Exists)
            {
                directory.Delete(true);
            }
        }

        public IReadOnlyDictionary<string, IReadOnlyCollection<CommitInfo>> GetCommitsCache(Repository repository, Predicate<string> filter, CancellationToken cancellationToken)
        {
            var commitsCache = new Dictionary<string, List<CommitInfo>>();

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = _gitExecutable,
                    Arguments = @"log --pretty=format:""commit:%H%nauthor_name:%an%nauthor_email:%ae%ndate:%ai"" --name-only --first-parent",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    WorkingDirectory = repository.Info.WorkingDirectory
                }
            };

            string? currentLine;
            string? sha = default;
            string? authorName = default;
            string? authorEmail = default;
            DateTimeOffset commitDate = default;
            List<string> files = new List<string>();

            process.Start();

            void AddFilesToCache()
            {
                foreach (var file in files)
                {
                    if (!commitsCache!.TryGetValue(file, out var list))
                    {
                        list = new List<CommitInfo>();
                        commitsCache.Add(file, list);
                    }

                    list.Add(new CommitInfo(authorName!, authorEmail!, commitDate, sha!));
                }

                files.Clear();
            }

            while ((currentLine = process.StandardOutput.ReadLine()) != null)
            {
                var parts = currentLine.Split(':');
                switch (parts[0])
                {
                    case "commit":
                        sha = currentLine.Substring(parts[0].Length + 1);
                        break;
                    case "author_name":
                        authorName = currentLine.Substring(parts[0].Length + 1);
                        break;
                    case "author_email":
                        authorEmail = currentLine.Substring(parts[0].Length + 1);
                        break;
                    case "date":
                        commitDate = DateTimeOffset.Parse(currentLine.Substring(parts[0].Length + 1));
                        break;
                    case "":
                        AddFilesToCache();
                        break;
                    default:
                        if (filter(currentLine))
                        {
                            files.Add(currentLine);
                        }
                        break;
                }
            }

            AddFilesToCache();

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new Exception($"git returned non-zero exit code ({process.ExitCode})");
            }

            return new ReadOnlyDictionary<string, IReadOnlyCollection<CommitInfo>>(commitsCache.ToDictionary(x => x.Key, x => (IReadOnlyCollection<CommitInfo>)x.Value));
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
            }
        }

        private void CloneRepository(Uri uri, string repositoryRoot, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Cloning repository '{uri}' in '{repositoryRoot}'");

            var cloneOptions = CreateOptions<CloneOptions>(cancellationToken);
            cloneOptions.RecurseSubmodules = false;
            Repository.Clone(uri.AbsoluteUri, repositoryRoot, cloneOptions);
        }

        private T CreateOptions<T>(CancellationToken cancellationToken)
            where T : FetchOptionsBase, new()
        {
            var options = new T();
            options.OnProgress = x => !cancellationToken.IsCancellationRequested;
            options.OnTransferProgress = x => !cancellationToken.IsCancellationRequested;

            return options;
        }

        private string GetGitExecutable()
        {
            // Azure function hosts don't ship git so we use local packaged version
            var executionDirectory = Path.GetDirectoryName(GetType().Assembly.Location)!;
            var localGitExecutable = Path.Combine(executionDirectory, "GitWindowsMinimal", "mingw64", "bin", "git.exe");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(localGitExecutable))
            {
                _logger.LogDebug($"Using git from {localGitExecutable}");
                return localGitExecutable;
            }
            else
            {
                _logger.LogDebug($"Using git from PATH");
                return "git";
            }
        }
    }
}
