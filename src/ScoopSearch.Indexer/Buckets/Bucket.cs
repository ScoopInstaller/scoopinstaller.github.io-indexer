namespace ScoopSearch.Indexer.Buckets;

public class Bucket
{
    public Bucket(Uri uri, int stars)
    {
        Uri = new Uri(uri.AbsoluteUri.ToLowerInvariant());
        Stars = stars;
    }

    public Uri Uri { get; private set; }

    public int Stars { get; private set; }
}
