using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using ScoopSearch.Functions.Data;

namespace ScoopSearch.Functions.Function.Indexing;

public class Orchestrator
{
    [FunctionName("RunOrchestrator")]
    public async Task<string> RunOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext context,
        ILogger logger)
    {
        logger = context.CreateReplaySafeLogger(logger);
        logger.LogInformation("Starting orchestration");


        var bucketInfoList = await context.CallActivityAsync<BucketInfo[]>(nameof(FetchBucketsActivity.FetchBuckets), null);
        logger.LogInformation("Received {0} buckets to crawl", bucketInfoList.Length);

        var tasks = new List<Task>();
        foreach (var bucketInfo in bucketInfoList)
        {
            tasks.Add(context.CallActivityAsync(nameof(BucketCrawlerActivity.CrawlBucket), bucketInfo));
        }

        await Task.WhenAll(tasks);

        return context.InstanceId;
    }
}
