namespace ScoopSearch.Indexer.Git;

public class CommitInfo
{
    public CommitInfo(DateTimeOffset date, string sha)
    {
        Date = date;
        Sha = sha;
    }

    public DateTimeOffset Date { get; }

    public string Sha { get; }
}
