using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.GitHub;

namespace ScoopSearch.Indexer.Processor;

internal class FetchBucketsProcessor : IFetchBucketsProcessor
{
    private readonly IGitHubClient _gitHubClient;
    private readonly ILogger _logger;
    private readonly BucketsOptions _bucketOptions;

    public FetchBucketsProcessor(
        IGitHubClient gitHubClient,
        IOptions<BucketsOptions> bucketOptions,
        ILogger<FetchBucketsProcessor> logger)
    {
        _gitHubClient = gitHubClient;
        _logger = logger;
        _bucketOptions = bucketOptions.Value;
    }

    public async Task<BucketInfo[]> FetchBucketsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving buckets from sources");
        var officialBucketsTask = RetrieveOfficialBucketsAsync(cancellationToken);
        var githubBucketsTask = SearchForBucketsOnGitHubAsync(cancellationToken);
        var ignoredBucketsTask = RetrieveBucketsFromListAsync(_bucketOptions.IgnoredBucketsListUrl, false, cancellationToken);
        var manualBucketsTask = RetrieveBucketsFromListAsync(_bucketOptions.ManualBucketsListUrl, true, cancellationToken);

        await Task.WhenAll(officialBucketsTask, githubBucketsTask, ignoredBucketsTask, manualBucketsTask);

        _logger.LogInformation("Found {Count} official buckets ({Url})", officialBucketsTask.Result.Count(), _bucketOptions.OfficialBucketsListUrl);
        _logger.LogInformation("Found {Count} buckets on GitHub", githubBucketsTask.Result.Count);
        _logger.LogInformation("Found {Count} buckets to ignore (appsettings.json)", _bucketOptions.IgnoredBuckets.Count);
        _logger.LogInformation("Found {Count} buckets to ignore from external list ({Url})", ignoredBucketsTask.Result.Count(), _bucketOptions.IgnoredBucketsListUrl);
        _logger.LogInformation("Found {Count} buckets to add (appsettings.json)", _bucketOptions.ManualBuckets.Count);
        _logger.LogInformation("Found {Count} buckets to add from external list ({Url})", manualBucketsTask.Result.Count(), _bucketOptions.ManualBucketsListUrl);

        var allBuckets = githubBucketsTask.Result.Keys
            .Concat(officialBucketsTask.Result)
            .Concat(_bucketOptions.ManualBuckets)
            .Concat(manualBucketsTask.Result)
            .Except(_bucketOptions.IgnoredBuckets)
            .Except(ignoredBucketsTask.Result)
            .Where(_gitHubClient.IsValidRepositoryDomain)
            .DistinctBy(_ => _.AbsoluteUri.ToLowerInvariant())
            .ToHashSet();

        _logger.LogInformation("{Count} buckets found for indexing", allBuckets.Count);
        var bucketsToIndexTasks = allBuckets.Select(async _ =>
        {
            var stars = githubBucketsTask.Result.TryGetValue(_, out var value) ? value : (await _gitHubClient.GetRepositoryAsync(_, cancellationToken))?.Stars ?? -1;
            var official = officialBucketsTask.Result.Contains(_);
            _logger.LogDebug("Adding bucket '{Url}' (stars: {Stars}, official: {Official})", _, stars, official);

            return new BucketInfo(_, stars, official);
        }).ToArray();

        return await Task.WhenAll(bucketsToIndexTasks);
    }

    private async Task<IEnumerable<Uri>> RetrieveOfficialBucketsAsync(CancellationToken cancellationToken)
    {
        var contentJson = await _gitHubClient.GetAsStringAsync(_bucketOptions.OfficialBucketsListUrl, cancellationToken);
        var officialBuckets = JsonSerializer.Deserialize<Dictionary<string, string>>(contentJson)?.Values;

        return officialBuckets?.Select(_ => new Uri(_)).ToHashSet() ?? Enumerable.Empty<Uri>();
    }

    private async Task<IEnumerable<Uri>> RetrieveBucketsFromListAsync(Uri bucketsList, bool followRedirects, CancellationToken cancellationToken)
    {
        ConcurrentBag<Uri> buckets = new();

        var tasks = new List<Task>();
        await foreach (var uri in GetBucketsFromList(bucketsList, cancellationToken))
        {
            tasks.Add(GetTargetRepository(uri, followRedirects, cancellationToken)
                .ContinueWith(t => { if (t.Result != null) { buckets.Add(t.Result); } }, cancellationToken));
        }

        await Task.WhenAll(tasks);

        return buckets;
    }

    private async IAsyncEnumerable<Uri> GetBucketsFromList(Uri bucketsList, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var content = await _gitHubClient.GetAsStringAsync(bucketsList, cancellationToken);
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
                uri = uri.Substring(0, uri.Length - 4);
            }

            yield return new Uri(uri);
        }
    }

    private async Task<Uri?> GetTargetRepository(Uri uri, bool followRedirects, CancellationToken cancellationToken)
    {
        // Validate uri (existing repository, follow redirections...)
        using var request = new HttpRequestMessage(HttpMethod.Head, uri);
        using var response = await _gitHubClient.SendAsync(request, followRedirects, cancellationToken);

        if (request.RequestUri != null)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Skipping '{Uri}' because it returns '{Status}' status", uri, response.StatusCode);
                return null;
            }

            if (request.RequestUri != uri)
            {
                _logger.LogDebug("'{Uri}' redirects to '{RedirectUri}'", uri, request.RequestUri);
            }

            return request.RequestUri;
        }

        return null;
    }

    private async Task<IDictionary<Uri, int>> SearchForBucketsOnGitHubAsync(CancellationToken cancellationToken)
    {
        ConcurrentDictionary<Uri, int> buckets = new ConcurrentDictionary<Uri, int>();
        await Parallel.ForEachAsync(_bucketOptions.GithubBucketsSearchQueries,
            new ParallelOptions() { CancellationToken = cancellationToken },
            async (gitHubSearchQuery, token) =>
            {
                await foreach (var repository in _gitHubClient.SearchRepositoriesAsync(gitHubSearchQuery, token))
                {
                    buckets[repository.HtmlUri] = repository.Stars;
                }
            });

        return buckets;
    }
}
