using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

            var officialBuckets = await officialBucketsTask;
            var githubBuckets = await githubBucketsTask;
            logger.LogInformation($"Found {officialBuckets.Count} official buckets.");
            logger.LogInformation($"Found {githubBuckets.Count} buckets on GitHub.");

            var allBuckets = githubBuckets.Keys
                .Concat(officialBuckets)
                .Concat(_bucketOptions.ManualBuckets)
                .Except(_bucketOptions.IgnoredBuckets)
                .ToHashSet();

            await CleanIndexFromNonExistentBucketsAsync(allBuckets, logger, cancellationToken);

            var bucketsToIndex = allBuckets.Select(x =>
            {
                var stars = githubBuckets.TryGetValue(x, out var value) ? value : -1;
                var official = officialBuckets.Contains(x);
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

        private async Task<IDictionary<Uri, int>> SearchForBucketsOnGitHubAsync(CancellationToken cancellationToken)
        {
            // TODO : Use GitHub API v4
            ConcurrentDictionary<Uri, int> buckets = new ConcurrentDictionary<Uri, int>();
            List<Task> tasks = new List<Task>();
            foreach (var gitHubSearchQuery in _bucketOptions.GithubBucketsSearchQueries)
            {
                tasks.Add(Task.Run(async () =>
                {
                    // First query to retrieve total_count and first results
                    var searchUri = new Uri($"{gitHubSearchQuery}&per_page={ResultsPerPage}&page=1");
                    var firstResults = await GetGitHubSearchResultsAsync(searchUri, cancellationToken);

                    // Using TotalCount, parallelize the remaining queries for all the remaining pages of results
                    var totalPages = (int)Math.Ceiling(firstResults.TotalCount / (double)ResultsPerPage);
                    var remainingResultsTasks = Enumerable.Range(2, totalPages).Select(page =>
                    {
                        var searchUri = new Uri($"{gitHubSearchQuery}&per_page={ResultsPerPage}&page={page}");
                        return GetGitHubSearchResultsAsync(searchUri, cancellationToken);
                    }).ToArray();

                    await Task.WhenAll(remainingResultsTasks);

                    var allResultsItems = firstResults.Items.Concat(remainingResultsTasks.SelectMany(x => x.Result.Items));
                    foreach (var item in allResultsItems)
                    {
                        buckets[item.HtmlUri] = item.Stars;
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
            return buckets;
        }

        private Task<GitHubSearchResults> GetGitHubSearchResultsAsync(Uri searchUri, CancellationToken cancellationToken)
        {
            return GetAsStringAsync(searchUri, cancellationToken)
                .ContinueWith(task
                    => JsonConvert.DeserializeObject<GitHubSearchResults>(task.Result), cancellationToken);
        }

        private async Task<string> GetAsStringAsync(Uri uri, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            using (var response = await _githubHttpClient.SendAsync(request, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
        }

        private Task QueueBucketsForIndexingAsync(IAsyncCollector<QueueItem> queue, QueueItem[] items, ILogger logger, CancellationToken cancellationToken)
        {
            logger.LogInformation($"Adding {items.Length} buckets for indexing.");
            List<Task> tasks = new List<Task>();
            foreach (var item in items)
            {
                tasks.Add(Task.Run(async () =>
                {
                    logger.LogDebug($"Adding bucket '{item.Bucket}' (stars: {item.Stars}, official: {item.Official}) to queue.");
                    await queue.AddAsync(item, cancellationToken);
                }, cancellationToken));
            }

            return Task.WhenAll(tasks);
        }

        private async Task CleanIndexFromNonExistentBucketsAsync(IEnumerable<Uri> buckets, ILogger logger, CancellationToken cancellationToken)
        {
            var allBucketsFromIndex = await _indexer.GetBucketsAsync(cancellationToken);
            var deletedBuckets = allBucketsFromIndex.Except(buckets).ToArray();
            logger.LogInformation($"{deletedBuckets.Length} buckets to remove from the index.");

            List<Task> tasks = new List<Task>();
            foreach (var deletedBucket in deletedBuckets.TakeWhile(x => !cancellationToken.IsCancellationRequested))
            {
                tasks.Add(Task.Run(async () =>
                {
                    var manifests = (await _indexer.GetExistingManifestsAsync(deletedBucket, cancellationToken)).ToArray();
                    logger.LogDebug($"Deleting {manifests.Length} manifests from bucket {deletedBucket}.");
                    await _indexer.DeleteManifestsAsync(manifests, cancellationToken);
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }
}
