using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoopSearch.Indexer.Buckets.Providers;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.Extensions;

namespace ScoopSearch.Indexer.Buckets.Sources;

internal class OfficialBucketsSource : IOfficialBucketsSource
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEnumerable<IBucketsProvider> _bucketsProviders;
    private readonly BucketsOptions _bucketOptions;
    private readonly ILogger _logger;

    public OfficialBucketsSource(
        IHttpClientFactory httpClientFactory,
        IEnumerable<IBucketsProvider> bucketsProviders,
        IOptions<BucketsOptions> bucketOptions,
        ILogger<GitHubBucketsProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _bucketsProviders = bucketsProviders;
        _logger = logger;
        _bucketOptions = bucketOptions.Value;
    }

    public async IAsyncEnumerable<Bucket> GetBucketsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_bucketOptions.OfficialBucketsListUrl is null)
        {
            _logger.LogWarning("No official buckets list url found in configuration");
            yield break;
        }

        _logger.LogInformation("Retrieving official buckets from '{Uri}'", _bucketOptions.OfficialBucketsListUrl);

        await foreach (var uri in GetBucketsFromJsonAsync(_bucketOptions.OfficialBucketsListUrl, cancellationToken))
        {
            var provider = _bucketsProviders.FirstOrDefault(provider => provider.IsCompatible(uri));
            if (provider is not null && await provider.GetBucketAsync(uri, cancellationToken) is { } bucket)
            {
                yield return bucket;
            }
        }
    }

    private async IAsyncEnumerable<Uri> GetBucketsFromJsonAsync(Uri uri, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var contentJson = await _httpClientFactory.CreateDefaultClient().GetStreamAsync(uri, cancellationToken);
        var officialBuckets = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(contentJson, cancellationToken: cancellationToken);
        if (officialBuckets is null)
        {
            _logger.LogWarning("Unable to parse buckets list from '{Uri}'", uri);
            yield break;
        }

        foreach (var officialBucket in officialBuckets)
        {
            yield return new Uri(officialBucket.Value);
        }
    }
}
