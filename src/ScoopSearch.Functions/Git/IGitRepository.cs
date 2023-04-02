using System;
using System.Collections.Generic;
using System.Threading;
using LibGit2Sharp;
using ScoopSearch.Functions.Data;

namespace ScoopSearch.Functions.Git
{
    public interface IGitRepository
    {
        string? DownloadRepository(Uri uri, CancellationToken cancellationToken);

        void DeleteRepository(string repository);

        IReadOnlyDictionary<string, IReadOnlyCollection<CommitInfo>> GetCommitsCache(Repository repository, Predicate<string> filter, CancellationToken cancellationToken);
    }
}
