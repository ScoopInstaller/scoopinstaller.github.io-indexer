using System;
using System.Collections.Generic;
using System.Threading;
using LibGit2Sharp;

namespace ScoopSearch.Functions.Git
{
    public interface IGitRepository
    {
        string DownloadRepository(Uri uri, CancellationToken cancellationToken);

        void DeleteRepository(string repository);

        IDictionary<string, Commit[]> GetCommitsCache(Repository repository, Predicate<string> filter, CancellationToken cancellationToken);
    }
}
