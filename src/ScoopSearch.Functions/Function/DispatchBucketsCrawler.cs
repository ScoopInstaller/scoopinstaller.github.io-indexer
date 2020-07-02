using System;
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
            var officialBuckets = (await RetrieveOfficialBucketsAsync(cancellationToken)).ToHashSet();
            logger.LogDebug($"Found {officialBuckets.Count} official buckets");

            var githubFoundBuckets = (await SearchAllBucketsOnGitHub(cancellationToken));
            logger.LogDebug($"Found {githubFoundBuckets.Count} buckets on GitHub");

            var allBuckets = githubFoundBuckets.Keys
                .Concat(officialBuckets)
                .Concat(_bucketOptions.ManualBuckets)
                .Except(_bucketOptions.IgnoredBuckets)
                .ToHashSet();

            await DeleteRemovedBucketsFromIndexAsync(allBuckets, logger, cancellationToken);

            var bucketsToQueue = allBuckets.Select(x =>
            {
                var stars = githubFoundBuckets.TryGetValue(x, out var value) ? value : -1;
                var official = officialBuckets.Contains(x);
                return new QueueItem(x, stars, official);
            });

            await QueueBucketsForCrawlingAsync(queue, bucketsToQueue, logger, cancellationToken);
        }

        private async Task<IEnumerable<Uri>> RetrieveOfficialBucketsAsync(CancellationToken cancellationToken)
        {
            var contentJson = await GetStringAsync(_bucketOptions.OfficialBucketsListUrl, cancellationToken);
            var officialBuckets = JsonConvert.DeserializeObject<Dictionary<string, string>>(contentJson).Values;

            return officialBuckets.Select(x => new Uri(x));
        }

        private async Task<IDictionary<Uri, int>> SearchAllBucketsOnGitHub(CancellationToken cancellationToken)
        {
            // TODO : Use GitHub API v4
            Dictionary<Uri, int> buckets = new Dictionary<Uri, int>();
            var resultJsonTemplate = new {total_count = 0, items = new[] {new {html_url = "", stargazers_count = 0}}};

            foreach (var gitHubSearchQuery in _bucketOptions.GithubBucketsSearchQueries
                .TakeWhile(x => !cancellationToken.IsCancellationRequested))
            {
                int page = 1;
                while (true)
                {
                    var searchUrl = new Uri($"{gitHubSearchQuery}&per_page=100&page={page++}");
                    var contentJson = await GetStringAsync(searchUrl, cancellationToken);
                    var result = JsonConvert.DeserializeAnonymousType(contentJson, resultJsonTemplate);

                    foreach (var item in result.items)
                    {
                        buckets[new Uri(item.html_url)] = item.stargazers_count;
                    }

                    if (result.items.Length == 0)
                    {
                        break;
                    }
                }
            }

            return buckets;
        }

        private async Task<string> GetStringAsync(Uri uri, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            using (var response = await _githubHttpClient.SendAsync(request, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
        }

        private async Task QueueBucketsForCrawlingAsync(IAsyncCollector<QueueItem> queue, IEnumerable<QueueItem> buckets, ILogger logger, CancellationToken cancellationToken)
        {
            foreach (var bucket in buckets.TakeWhile(x => !cancellationToken.IsCancellationRequested))
            {
                logger.LogInformation($"Adding bucket '{bucket.Bucket}' (stars: {bucket.Stars}, official: {bucket.Official}) to queue");
                await queue.AddAsync(bucket, cancellationToken);
            }
        }

        private async Task DeleteRemovedBucketsFromIndexAsync(IEnumerable<Uri> buckets, ILogger logger, CancellationToken cancellationToken)
        {
            var allBucketsFromIndex = await _indexer.GetBucketsAsync(cancellationToken);
            var deletedBuckets = allBucketsFromIndex.Except(buckets).ToArray();
            foreach (var deletedBucket in deletedBuckets.TakeWhile(x => !cancellationToken.IsCancellationRequested))
            {
                var manifests = (await _indexer.GetExistingManifestsAsync(deletedBucket, cancellationToken)).ToArray();
                logger.LogInformation($"Deleting {manifests.Length} manifests from bucket {deletedBucket}.");
                await _indexer.DeleteManifestsAsync(manifests, cancellationToken);
            }
        }
    }
}
