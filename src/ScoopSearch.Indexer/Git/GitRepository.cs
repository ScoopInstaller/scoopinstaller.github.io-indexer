using System.Collections.ObjectModel;
using System.Diagnostics;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace ScoopSearch.Indexer.Git;

internal class GitRepository : IGitRepository, IDisposable
{
    private readonly Repository _repository;
    private readonly string _gitExecutable;
    private readonly ILogger _logger;

    public GitRepository(string repositoryDirectory, string gitExecutable, ILogger logger)
    {
        _repository = new Repository(repositoryDirectory);
        _gitExecutable = gitExecutable;
        _logger = logger;
    }

    public void Dispose() => _repository.Dispose();

    public void Delete()
    {
        _logger.LogDebug("Deleting repository {WorkingDirectory}", _repository.Info.WorkingDirectory);

        var workingDirectory = _repository.Info.WorkingDirectory;
        _repository.Dispose();

        var directory = new DirectoryInfo(workingDirectory);
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

    public async Task<IReadOnlyDictionary<string, IReadOnlyCollection<CommitInfo>>> GetCommitsCacheAsync(Predicate<string> filter, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Computing commits cache for repository {WorkingDirectory}", _repository.Info.WorkingDirectory);

        var commitsCache = new Dictionary<string, List<CommitInfo>>();

        var process = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = _gitExecutable,
                Arguments = @"log --pretty=format:""commit:%H%ndate:%ai"" --name-only --first-parent",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WorkingDirectory = _repository.Info.WorkingDirectory
            }
        };

        string? sha = default;
        DateTimeOffset commitDate = default;
        List<string> files = new List<string>();

        process.Start();

        void AddFilesToCache()
        {
            foreach (var file in files)
            {
                if (!commitsCache.TryGetValue(file, out var list))
                {
                    list = new List<CommitInfo>();
                    commitsCache.Add(file, list);
                }

                list.Add(new CommitInfo(commitDate, sha!));
            }

            files.Clear();
        }

        while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } currentLine && !cancellationToken.IsCancellationRequested)
        {
            var parts = currentLine.Split(':');
            switch (parts[0])
            {
                case "commit":
                    sha = currentLine.Substring(parts[0].Length + 1);
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

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git returned non-zero exit code ({process.ExitCode})");
        }

        _logger.LogDebug("Cache computed for repository {WorkingDirectory}: {Count} files", _repository.Info.WorkingDirectory, commitsCache.Count);

        return new ReadOnlyDictionary<string, IReadOnlyCollection<CommitInfo>>(commitsCache.ToDictionary(_ => _.Key, _ => (IReadOnlyCollection<CommitInfo>)_.Value));
    }

    public string GetBranchName()
    {
        return _repository.Head.FriendlyName;
    }

    public IEnumerable<string> GetFilesFromIndex()
    {
        return _repository.Index
            .Where(_ => _.Mode is Mode.NonExecutableFile)
            .Select(_ => _.Path);
    }

    public Task<string> ReadContentAsync(string filePath, CancellationToken cancellationToken)
    {
        return File.ReadAllTextAsync(Path.Combine(_repository.Info.WorkingDirectory, filePath), cancellationToken);
    }
}
