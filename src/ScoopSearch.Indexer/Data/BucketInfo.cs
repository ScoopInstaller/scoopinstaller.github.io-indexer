using System.Text.Json.Serialization;

namespace ScoopSearch.Indexer.Data;

public class BucketInfo
{
    public BucketInfo(Uri uri, int stars, bool official)
    {
        Uri = uri;
        Stars = stars;
        Official = official;
    }

    [JsonInclude]
    public Uri Uri { get; private set; }

    [JsonInclude]
    public int Stars { get; private set; }

    [JsonInclude]
    public bool Official { get; private set; }
}
