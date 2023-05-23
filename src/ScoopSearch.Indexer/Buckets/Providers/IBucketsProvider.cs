namespace ScoopSearch.Indexer.Buckets.Providers;

public interface IBucketsProvider
{
    Task<Bucket?> GetBucketAsync(Uri uri, CancellationToken cancellationToken);

    bool IsCompatible(Uri uri);
}
