namespace ScoopSearch.Indexer.Configuration;

public class BucketsOptions
{
    public const string Key = "Buckets";

    public Uri OfficialBucketsListUrl { get; set; } = null!;

    public string[] GithubBucketsSearchQueries { get; set; } = {};

    public HashSet<Uri> IgnoredBuckets { get; set; } = new HashSet<Uri>();

    public Uri IgnoredBucketsListUrl { get; set; } = null!;

    public HashSet<Uri> ManualBuckets { get; set; } = new HashSet<Uri>();

    public Uri ManualBucketsListUrl { get; set; } = null!;
}
