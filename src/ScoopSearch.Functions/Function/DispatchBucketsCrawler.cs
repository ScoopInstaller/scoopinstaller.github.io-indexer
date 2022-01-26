using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ScoopSearch.Functions.Configuration;
using ScoopSearch.Functions.Data;
using ScoopSearch.Functions.Indexer;

namespace ScoopSearch.Functions.Function
{
    public class DispatchBucketsCrawler
    {
        private const int ResultsPerPage = 100;
        private const int MaxDegreeOfParallelism = 8;

        private readonly HttpClient _githubHttpClient;
        private readonly IIndexer _indexer;
        private readonly BucketsOptions _bucketOptions;

        public DispatchBucketsCrawler(
            IHttpClientFactory httpClientFactory,
            IIndexer indexer,
            IOptions<BucketsOptions> bucketOptions)
        {
            _githubHttpClient = httpClientFactory.CreateClient(Constants.GitHubHttpClientName);
            _indexer = indexer;
            _bucketOptions = bucketOptions.Value;
        }

        [FunctionName("DispatchBucketsCrawler")]
        public async Task Run(
            [TimerTrigger("%DispatchBucketsCrawlerCron%")] TimerInfo timer,
            [Queue(Constants.BucketsQueue)] IAsyncCollector<QueueItem> queue,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var officialBucketsTask = RetrieveOfficialBucketsAsync(cancellationToken);
            var githubBucketsTask = SearchForBucketsOnGitHubAsync(cancellationToken);
            var ignoredBucketsTask = RetrieveBucketsAsync(this._bucketOptions.IgnoredBucketsListUrl, logger, cancellationToken);
            var manualBucketsTask = RetrieveBucketsAsync(this._bucketOptions.ManualBucketsListUrl, logger, cancellationToken);

            await Task.WhenAll(officialBucketsTask, githubBucketsTask, ignoredBucketsTask, manualBucketsTask);

            logger.LogInformation($"Found {officialBucketsTask.Result.Count} official buckets.");
            logger.LogInformation($"Found {githubBucketsTask.Result.Count} buckets on GitHub.");
            logger.LogInformation($"Found {_bucketOptions.IgnoredBuckets.Count} buckets to ignore.");
            logger.LogInformation($"Found {ignoredBucketsTask.Result.Count} buckets to ignore from external list.");
            logger.LogInformation($"Found {_bucketOptions.ManualBuckets.Count} buckets to manually add.");
            logger.LogInformation($"Found {manualBucketsTask.Result.Count} buckets to manually add from external list.");

            var allBuckets = githubBucketsTask.Result.Keys
                .Concat(officialBucketsTask.Result)
                .Concat(_bucketOptions.ManualBuckets)
                .Concat(manualBucketsTask.Result)
                .Except(_bucketOptions.IgnoredBuckets)
                .Except(ignoredBucketsTask.Result)
                .ToHashSet();

            await CleanIndexFromNonExistentBucketsAsync(allBuckets, logger, cancellationToken);

            var bucketsToIndex = allBuckets.Select(x =>
            {
                var stars = githubBucketsTask.Result.TryGetValue(x, out var value) ? value : -1;
                var official = officialBucketsTask.Result.Contains(x);
                return new QueueItem(x, stars, official);
            }).ToArray();

            await QueueBucketsForIndexingAsync(queue, bucketsToIndex, logger, cancellationToken);
        }

        private async Task<HashSet<Uri>> RetrieveOfficialBucketsAsync(CancellationToken cancellationToken)
        {
            var contentJson = await GetAsStringAsync(_bucketOptions.OfficialBucketsListUrl, cancellationToken);
            var officialBuckets = JsonConvert.DeserializeObject<Dictionary<string, string>>(contentJson).Values;

            return officialBuckets.Select(x => new Uri(x)).ToHashSet();
        }

        private async Task<HashSet<Uri>> RetrieveBucketsAsync(Uri bucketsList, ILogger logger, CancellationToken cancellationToken)
        {
            HashSet<Uri> buckets = new HashSet<Uri>();
            try
            {
                var content = await GetAsStringAsync(bucketsList, cancellationToken);
                using (var csv = new CsvHelper.CsvReader(new StringReader(content), CultureInfo.InvariantCulture))
                {
                    csv.Read();
                    var r = csv.ReadHeader();

                    while (csv.Read())
                    {
                        var record = csv.GetField<string>("url");
                        if (record.EndsWith(".git"))
                        {
                            record = record.Substring(0, record.Length - 4);
                        }

                        buckets.Add(new Uri(record));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to read/parse data from '{0}'", bucketsList);
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
                    var firstResults = await GetGitHubSearchResultsAsync(firstSearchUri, cancellationToken);
                    firstResults.Items.ForEach(item => buckets[item.HtmlUri] = item.Stars);

                    // Using TotalCount, parallelize the remaining queries for all the remaining pages of results
                    var totalPages = (int)Math.Ceiling(firstResults.TotalCount / (double)ResultsPerPage);
                    for (int page = 2; page <= totalPages; page++)
                    {
                        var searchUri = new Uri($"{gitHubSearchQuery}&per_page={ResultsPerPage}&page={page}&sort=updated");
                        var results = await GetGitHubSearchResultsAsync(searchUri, cancellationToken);
                        results.Items.ForEach(item => buckets[item.HtmlUri] = item.Stars);
                    }
                });

            return buckets;
        }

        private async Task<GitHubSearchResults> GetGitHubSearchResultsAsync(Uri searchUri, CancellationToken cancellationToken)
        {
            return await GetAsStringAsync(searchUri, cancellationToken)
                .ContinueWith(task
                    => JsonConvert.DeserializeObject<GitHubSearchResults>(task.Result), cancellationToken);
        }

        private async Task<string> GetAsStringAsync(Uri uri, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            using (var response = await _githubHttpClient.SendAsync(request, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
        }

        private async Task QueueBucketsForIndexingAsync(IAsyncCollector<QueueItem> queue, QueueItem[] items, ILogger logger, CancellationToken cancellationToken)
        {
            logger.LogInformation($"Adding {items.Length} buckets for indexing.");
            await Parallel.ForEachAsync(
                items,
                new ParallelOptions() { MaxDegreeOfParallelism = MaxDegreeOfParallelism, CancellationToken = cancellationToken},
                async (item, cancellationToken) =>
                {
                    logger.LogDebug($"Adding bucket '{item.Bucket}' (stars: {item.Stars}, official: {item.Official}) to queue.");
                    await queue.AddAsync(item, cancellationToken);
                });
        }

        private async Task CleanIndexFromNonExistentBucketsAsync(IEnumerable<Uri> buckets, ILogger logger, CancellationToken cancellationToken)
        {
            var allBucketsFromIndex = await _indexer.GetBucketsAsync(cancellationToken);
            var deletedBuckets = allBucketsFromIndex.Except(buckets).ToArray();
            logger.LogInformation($"{deletedBuckets.Length} buckets to remove from the index.");

            await Parallel.ForEachAsync(
                deletedBuckets.TakeWhile(x => !cancellationToken.IsCancellationRequested),
                new ParallelOptions() { MaxDegreeOfParallelism = MaxDegreeOfParallelism, CancellationToken = cancellationToken },
                async (deletedBucket, cancellationToken) =>
                {
                    var manifests = (await _indexer.GetExistingManifestsAsync(deletedBucket, cancellationToken)).ToArray();
                    logger.LogDebug($"Deleting {manifests.Length} manifests from bucket {deletedBucket}.");
                    await _indexer.DeleteManifestsAsync(manifests, cancellationToken);
                });
        }
    }
}
