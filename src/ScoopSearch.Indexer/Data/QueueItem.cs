namespace ScoopSearch.Indexer.Data;

public class QueueItem
{
    public QueueItem(Uri bucket, int stars, bool official)
    {
        Bucket = bucket;
        Stars = stars;
        Official = official;
    }

    public Uri Bucket { get; }

    public int Stars { get; }

    public bool Official { get; }
}
