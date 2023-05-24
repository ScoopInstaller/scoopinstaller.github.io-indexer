namespace ScoopSearch.Indexer.Buckets;

public class Bucket
{
    public Bucket(Uri uri, int stars)
    {
        Uri = uri;
        Stars = stars;
    }

    public Uri Uri { get; private set; }

    public int Stars { get; private set; }
}
