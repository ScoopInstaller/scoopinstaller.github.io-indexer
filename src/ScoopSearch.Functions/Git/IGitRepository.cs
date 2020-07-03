using System;
using System.Collections.Generic;
using System.Threading;
using LibGit2Sharp;

namespace ScoopSearch.Functions.Git
{
    public interface IGitRepository
    {
        string GetRepository(Uri uri, CancellationToken cancellationToken);

        void DeleteRepository(string repository);

        IDictionary<string, List<Commit>> GetCommitsCache(Repository repository, Predicate<string> filter);
    }
}
