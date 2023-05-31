using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoopSearch.Indexer.Buckets.Providers;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.Extensions;

namespace ScoopSearch.Indexer.Buckets.Sources;

internal class ManualBucketsListSource : IBucketsSource
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEnumerable<IBucketsProvider> _bucketsProviders;
    private readonly BucketsOptions _bucketOptions;
    private readonly ILogger _logger;

    public ManualBucketsListSource(
        IHttpClientFactory httpClientFactory,
        IEnumerable<IBucketsProvider> bucketsProviders,
        IOptions<BucketsOptions> bucketOptions,
        ILogger<ManualBucketsListSource> logger)
    {
        _httpClientFactory = httpClientFactory;
        _bucketsProviders = bucketsProviders;
        _bucketOptions = bucketOptions.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<Bucket> GetBucketsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_bucketOptions.ManualBucketsListUrl is null)
        {
            _logger.LogWarning("No buckets list url found in configuration");
            yield break;
        }

        var content = await _httpClientFactory.CreateDefaultClient().GetStringAsync(_bucketOptions.ManualBucketsListUrl, cancellationToken);
        using var csv = new CsvHelper.CsvReader(new StringReader(content), CultureInfo.InvariantCulture);
        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            var uri = csv.GetField<string>("url");
            if (uri == null)
            {
                continue;
            }

            if (uri.EndsWith(".git"))
            {
                uri = uri[..^4];
            }

            var bucketUri = new Uri(uri);
            var provider = _bucketsProviders.FirstOrDefault(provider => provider.IsCompatible(bucketUri));
            if (provider is not null && await provider.GetBucketAsync(bucketUri, cancellationToken) is { } bucket)
            {
                yield return bucket;
            }
        }
    }
}
