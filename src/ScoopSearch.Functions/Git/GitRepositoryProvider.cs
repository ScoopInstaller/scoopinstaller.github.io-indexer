using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace ScoopSearch.Functions.Git
{
    internal class GitRepositoryProvider : IGitRepositoryProvider
    {
        private static readonly Signature _signature = new Signature("Merge", "merge@example.com", DateTimeOffset.Now);

        private readonly ILogger<GitRepository> _logger;
        private readonly string _repositoriesDirectory;
        private readonly string _gitExecutable;

        public GitRepositoryProvider(ILogger<GitRepository> logger)
            : this(logger, Path.Combine(Path.GetTempPath(), "repositories"))
        {
        }

        internal GitRepositoryProvider(ILogger<GitRepository> logger, string repositoriesDirectory)
        {
            _logger = logger;
            _repositoriesDirectory = repositoriesDirectory;
            _gitExecutable = GetGitExecutable();
        }

        public IGitRepository? Download(Uri uri, CancellationToken cancellationToken)
        {
            var repositoryDirectory = Path.Combine(_repositoriesDirectory, uri.AbsolutePath[1..]); // Remove leading slash
            try
            {
                if (Directory.Exists(repositoryDirectory))
                {
                    try
                    {
                        PullRepository(repositoryDirectory, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Unable to pull repository '{Uri}' to '{RepositoryDirectory}'", uri, repositoryDirectory);
                        DeleteRepository(repositoryDirectory);
                        CloneRepository(uri, repositoryDirectory, cancellationToken);
                    }
                }
                else
                {
                    CloneRepository(uri, repositoryDirectory, cancellationToken);
                }

                using (var repository = new Repository(repositoryDirectory))
                {
                    if (repository.Head.Tip == null)
                    {
                        _logger.LogError("No valid branch found in '{RepositoryDirectory}'", repositoryDirectory);
                        return null;
                    }
                }

                return new GitRepository(repositoryDirectory, _gitExecutable, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to clone repository '{Uri}' to '{RepositoryDirectory}'", uri, repositoryDirectory);
                DeleteRepository(repositoryDirectory);
                return null;
            }
        }

        private void DeleteRepository(string repositoryDirectory)
        {
            var directory = new DirectoryInfo(repositoryDirectory);
            if (directory.Exists)
            {
                directory.Delete(true);
            }
        }

        private void PullRepository(string repositoryDirectory, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Pulling repository '{RepositoryDirectory}'", repositoryDirectory);

            using var repository = new Repository(repositoryDirectory);

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
                    throw new InvalidOperationException($"Merge conflict in repository {repositoryDirectory}");
                }
            }
        }

        private void CloneRepository(Uri uri, string repositoryDirectory, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Cloning repository '{Uri}' in '{RepositoryDirectory}'", uri, repositoryDirectory);

            var cloneOptions = CreateOptions<CloneOptions>(cancellationToken);
            cloneOptions.RecurseSubmodules = false;
            Repository.Clone(uri.AbsoluteUri, repositoryDirectory, cloneOptions);
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
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };

            try
            {
                if (process.Start())
                {
                    process.WaitForExit();
                    if (process.ExitCode == 0)
                    {
                        var version = process.StandardOutput.ReadLine();
                        _logger.LogDebug("Using git from PATH ({GitVersion})", version);
                        return process.StartInfo.FileName;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Git cannot be find in the path");
            }

            var executionDirectory = Path.GetDirectoryName(GetType().Assembly.Location)!;
            var localGitExecutable = Path.Combine(executionDirectory, "GitWindowsMinimal", "mingw64", "bin", "git.exe");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(localGitExecutable))
            {
                _logger.LogDebug("Using git from {LocalGitExecutable}", localGitExecutable);
                return localGitExecutable;
            }

            throw new Exception("Unable to find git executable");
        }
    }
}
