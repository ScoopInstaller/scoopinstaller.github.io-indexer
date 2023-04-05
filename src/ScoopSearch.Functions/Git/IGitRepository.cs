using System;
using System.Collections.Generic;
using System.Threading;
using ScoopSearch.Functions.Data;

namespace ScoopSearch.Functions.Git
{
    public record Entry(string Path, EntryType Type);

    public enum EntryType
    {
        File,
        Directory
    }

    public interface IGitRepository
    {
        void Delete();

        IReadOnlyDictionary<string, IReadOnlyCollection<CommitInfo>> GetCommitsCache(Predicate<string> filter, CancellationToken cancellationToken);

        string GetBranchName();

        IEnumerable<Entry> GetEntriesFromIndex();

        string ReadContent(Entry entry);
    }
}
