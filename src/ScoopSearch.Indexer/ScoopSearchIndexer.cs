using System.Collections.Concurrent;
using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.Processor;

namespace ScoopSearch.Indexer;

internal class ScoopSearchIndexer : IScoopSearchIndexer
{
    private readonly IFetchBucketsProcessor _fetchBucketsProcessor;
    private readonly IFetchManifestsProcessor _fetchManifestsProcessor;
    private readonly IIndexingProcessor _indexingProcessor;

    public ScoopSearchIndexer(IFetchBucketsProcessor fetchBucketsProcessor, IFetchManifestsProcessor fetchManifestsProcessor, IIndexingProcessor indexingProcessor)
    {
        _fetchBucketsProcessor = fetchBucketsProcessor;
        _fetchManifestsProcessor = fetchManifestsProcessor;
        _indexingProcessor = indexingProcessor;
    }

    public async Task ExecuteAsync()
    {
        var cancellationToken = CancellationToken.None;

        var buckets = await _fetchBucketsProcessor.FetchBucketsAsync(cancellationToken);
        var bucketsUrl = buckets.Select(_ => _.Uri).ToArray();

        ConcurrentBag<ManifestInfo[]> tasksResult = new();
        await Parallel.ForEachAsync(buckets, cancellationToken, async (bucket, _) =>
        {
            var result = await _fetchManifestsProcessor.FetchManifestsAsync(bucket, cancellationToken);
            tasksResult.Add(result);
        });

        await _indexingProcessor.CreateIndexIfRequiredAsync(cancellationToken);
        await _indexingProcessor.CleanIndexFromNonExistentBucketsAsync(bucketsUrl, cancellationToken);
        var manifests = tasksResult.SelectMany(_ => _).ToArray();
        await _indexingProcessor.UpdateIndexWithManifestsAsync(manifests, cancellationToken);
    }
}
