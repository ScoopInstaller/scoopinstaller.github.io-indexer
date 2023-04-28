using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.Extensions;
using ScoopSearch.Indexer.GitHub;

namespace ScoopSearch.Indexer.Processor;

internal class FetchBucketsProcessor : IFetchBucketsProcessor
{
    private const int ResultsPerPage = 100;
    private const int MaxDegreeOfParallelism = 8;

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
        var ignoredBucketsTask = RetrieveBucketsAsync(this._bucketOptions.IgnoredBucketsListUrl, false, cancellationToken);
        var manualBucketsTask = RetrieveBucketsAsync(this._bucketOptions.ManualBucketsListUrl, true, cancellationToken);

        await Task.WhenAll(officialBucketsTask, githubBucketsTask, ignoredBucketsTask, manualBucketsTask);

        _logger.LogInformation($"Found {officialBucketsTask.Result.Count} official buckets ({_bucketOptions.OfficialBucketsListUrl}).");
        _logger.LogInformation($"Found {githubBucketsTask.Result.Count} buckets on GitHub.");
        _logger.LogInformation($"Found {_bucketOptions.IgnoredBuckets.Count} buckets to ignore (appsettings.json).");
        _logger.LogInformation($"Found {ignoredBucketsTask.Result.Count} buckets to ignore from external list ({_bucketOptions.IgnoredBucketsListUrl}).");
        _logger.LogInformation($"Found {_bucketOptions.ManualBuckets.Count} buckets to add (appsettings.json).");
        _logger.LogInformation($"Found {manualBucketsTask.Result.Count} buckets to add from external list ({_bucketOptions.ManualBucketsListUrl}).");

        var allBuckets = githubBucketsTask.Result.Keys
            .Concat(officialBucketsTask.Result)
            .Concat(_bucketOptions.ManualBuckets)
            .Concat(manualBucketsTask.Result)
            .Except(_bucketOptions.IgnoredBuckets)
            .Except(ignoredBucketsTask.Result)
            .ToHashSet();

        _logger.LogInformation($"{allBuckets.Count} buckets found for indexing.");
        var bucketsToIndexTasks = allBuckets.Select(async _ =>
        {
            var stars = githubBucketsTask.Result.TryGetValue(_, out var value) ? value : (await _gitHubClient.GetRepoAsync(_, cancellationToken))?.Stars ?? -1;
            var official = officialBucketsTask.Result.Contains(_);
            _logger.LogDebug($"Adding bucket '{_}' (stars: {stars}, official: {official}).");

            return new BucketInfo(_, stars, official);
        }).ToArray();

        return await Task.WhenAll(bucketsToIndexTasks);
    }

    private async Task<HashSet<Uri>> RetrieveOfficialBucketsAsync(CancellationToken cancellationToken)
    {
        var contentJson = await _gitHubClient.GetAsStringAsync(_bucketOptions.OfficialBucketsListUrl, cancellationToken);
        var officialBuckets = JsonSerializer.Deserialize<Dictionary<string, string>>(contentJson)?.Values;

        return officialBuckets?.Select(_ => new Uri(_)).ToHashSet() ?? new HashSet<Uri>();
    }

    private async Task<HashSet<Uri>> RetrieveBucketsAsync(Uri bucketsList, bool followRedirects, CancellationToken cancellationToken)
    {
        HashSet<Uri> buckets = new HashSet<Uri>();
        try
        {
            var content = await _gitHubClient.GetAsStringAsync(bucketsList, cancellationToken);
            using (var csv = new CsvHelper.CsvReader(new StringReader(content), CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader();

                while (csv.Read())
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

                    // Validate uri (existing repository, follow redirections...)
                    using (var request = new HttpRequestMessage(HttpMethod.Head, uri))
                    using (var response = await _gitHubClient.SendAsync(request, followRedirects, cancellationToken))
                    {
                        if (request.RequestUri != null)
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                _logger.LogWarning($"Skipping '{uri}' because it returns '{(int)response.StatusCode}' status (from '{bucketsList}')");
                                continue;
                            }

                            if (request.RequestUri != new Uri(uri))
                            {
                                _logger.LogDebug($"'{uri}' redirects to '{request.RequestUri}' (from '{bucketsList}')");
                            }

                            buckets.Add(request.RequestUri);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to read/parse data from '{0}'", bucketsList);
        }

        return buckets;
    }

    private async Task<IDictionary<Uri, int>> SearchForBucketsOnGitHubAsync(CancellationToken cancellationToken)
    {
        // TODO : Use GitHub API v4
        ConcurrentDictionary<Uri, int> buckets = new ConcurrentDictionary<Uri, int>();
        await Parallel.ForEachAsync(_bucketOptions.GithubBucketsSearchQueries,
            new ParallelOptions() { MaxDegreeOfParallelism = MaxDegreeOfParallelism, CancellationToken = cancellationToken },
            async (gitHubSearchQuery, cancellationToken) =>
            {
                // First query to retrieve total_count and first results
                var firstSearchUri = new Uri($"{gitHubSearchQuery}&per_page={ResultsPerPage}&page=1&sort=updated");
                var firstResults = await _gitHubClient.GetSearchResultsAsync(firstSearchUri, cancellationToken);
                if (firstResults != null)
                {
                    firstResults.Items.ForEach(item => buckets[item.HtmlUri] = item.Stars);

                    // Using TotalCount, parallelize the remaining queries for all the remaining pages of results
                    var totalPages = (int)Math.Ceiling(firstResults.TotalCount / (double)ResultsPerPage);
                    for (int page = 2; page <= totalPages; page++)
                    {
                        var searchUri = new Uri($"{gitHubSearchQuery}&per_page={ResultsPerPage}&page={page}&sort=updated");
                        var results = await _gitHubClient.GetSearchResultsAsync(searchUri, cancellationToken);
                        results?.Items.ForEach(item => buckets[item.HtmlUri] = item.Stars);
                    }
                }
            });

        return buckets;
    }
}
