namespace ScoopSearch.Indexer.Configuration;

public class BucketsOptions
{
    public const string Key = "Buckets";

    public Uri? OfficialBucketsListUrl { get; set; }

    public Uri? ManualBucketsListUrl { get; set; }

    public HashSet<Uri>? IgnoredBuckets { get; set; }

    public Uri[]? ManualBuckets { get; set; }
}
