namespace ScoopSearch.Indexer.Data;

public class CommitInfo
{
    public CommitInfo(string authorName, string authorEmail, DateTimeOffset date, string sha)
    {
        AuthorName = authorName;
        AuthorEmail = authorEmail;
        Date = date;
        Sha = sha;
    }

    public string AuthorName { get; }

    public string AuthorEmail { get; }

    public DateTimeOffset Date { get; }

    public string Sha { get; }
}
