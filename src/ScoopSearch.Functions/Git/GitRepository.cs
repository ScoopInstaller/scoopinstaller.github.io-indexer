using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using LibGit2Sharp;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoopSearch.Functions.Data;

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

        public string? DownloadRepository(Uri uri, CancellationToken cancellationToken)
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

        public IDictionary<string, CommitInfo> GetCommitsCache(Repository repository, Predicate<string> filter, CancellationToken cancellationToken)
        {
            var processStartInfo = new ProcessStartInfo()
            {
                // Use git deployed with the Azure function or try to get git from the path
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location)!, "GitWindowsMinimal", "mingw64", "bin", "git.exe")
                    : "git",
                Arguments = @"log --pretty=format:""commit:%H%nauthor_name:%an%nauthor_email:%ae%ndate:%ai"" --name-only",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WorkingDirectory = repository.Info.WorkingDirectory
            };

            _logger.LogInformation("Using git from {gitPath}", processStartInfo.FileName);

            var process = new Process();
            process.StartInfo = processStartInfo;
            process.Start();

            var commitsCache = new Dictionary<string, CommitInfo>();
            string? currentLine;
            string? sha = default;
            string? authorName = default;
            string? authorEmail = default;
            DateTimeOffset commitDate = default;
            List<string> files = new List<string>();

            do
            {
                currentLine = process.StandardOutput.ReadLine();
                if (currentLine != null)
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
                            foreach (var file in files)
                            {
                                commitsCache.TryAdd(file, new CommitInfo(authorName!, authorEmail!, commitDate, sha!));
                            }

                            files.Clear();
                            break;

                        default:
                            if (filter(currentLine))
                            {
                                files.Add(currentLine);
                            }
                            break;
                    }
                }
            } while (currentLine != null);

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new Exception($"git.exe returned non-zero exit code ({process.ExitCode})");
            }

            return commitsCache;
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
