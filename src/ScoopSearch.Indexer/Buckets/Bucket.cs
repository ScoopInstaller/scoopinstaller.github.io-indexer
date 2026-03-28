namespace ScoopSearch.Indexer.Buckets;

public class Bucket
{
    public Bucket(Uri uri, int stars, string? name = null)
    {
        Uri = uri;
        Stars = stars;
        Name = name;
    }

    public Uri Uri { get; private set; }

    public int Stars { get; private set; }

    public string? Name { get; private set; }
}
