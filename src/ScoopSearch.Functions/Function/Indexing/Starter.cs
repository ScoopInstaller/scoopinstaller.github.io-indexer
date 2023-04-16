using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Polly;

namespace ScoopSearch.Functions.Function.Indexing;

public class Starter
{
    // Unique id used to identify the instance of the orchestrator and ensure a single instance is running at a time.
    private const string InstanceId = "0069DA7F-1890-49E8-8988-168071F52270";

    private readonly HttpClient _httpClient;

    public Starter(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    [FunctionName(nameof(KickoffStartOrchestrator))]
    public async Task KickoffStartOrchestrator(
        [TimerTrigger("%KickoffStartOrchestratorCron%")]
        TimerInfo timer,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var hostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
        var requestUri = $"http://{hostname}/api/{nameof(StartOrchestrator)}";

        logger.LogInformation("Calling {uri}", requestUri);

        await Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(
                5,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan) => logger.LogWarning(exception, "Error calling {uri}. Waiting for {duration}s", requestUri, timeSpan))
            .ExecuteAsync(() => _httpClient.GetAsync(requestUri, cancellationToken));
    }

    [FunctionName(nameof(StartOrchestrator))]
    public async Task<HttpResponseMessage> StartOrchestrator(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage request,
        [DurableClient] IDurableOrchestrationClient client,
        ILogger logger)
    {
        // Check if an instance with the specified ID already exists or an existing one stopped running(completed/failed/terminated).
        var existingInstance = await client.GetStatusAsync(InstanceId);
        if (existingInstance == null
            || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Completed
            || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Failed
            || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
        {
            // An instance with the specified ID doesn't exist or an existing one stopped running, create one.
            await client.StartNewAsync(nameof(Orchestrator.RunOrchestrator), InstanceId);
            logger.LogInformation($"Started orchestration with ID = '{InstanceId}'.");

            return client.CreateCheckStatusResponse(request, InstanceId);
        }
        else
        {
            // An instance with the specified ID exists or an existing one still running, don't create one.
            return new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent($"An instance with ID '{InstanceId}' already exists."),
            };
        }
    }
}
