using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoopSearch.Functions.Configuration;

namespace ScoopSearch.Functions.Function;

public class IndexingLog
{
    private static readonly TimeSpan LogsTimeRange = TimeSpan.FromHours(24);
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromSeconds(60);

    private readonly LogsQueryClient _client;
    private readonly string _workspaceId;

    public delegate LogsQueryClient LogsQueryClientFactory(TokenCredential tokenCredential);

    public IndexingLog(IOptions<AzureLogsMonitorOptions> options, LogsQueryClientFactory logsQueryClientFactory)
    {
        _workspaceId = options.Value.WorkspaceId;

        var token = new ClientSecretCredential(
            tenantId: options.Value.TenantId,
            clientId: options.Value.ClientId,
            clientSecret: options.Value.ClientSecret);

        _client = logsQueryClientFactory(token);
    }

    [FunctionName("IndexingLog")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest request,
        ILogger _)
    {
        var response = request.HttpContext.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/plain";
        response.Headers.Add("Cache-Control", $"public, no-store, max-age={(int)CacheMaxAge.TotalSeconds}");

        var streamWriter = new StreamWriter(response.Body);

        await foreach (var row in GetLogsAsync())
        {
            await streamWriter.WriteLineAsync(row);
        }

        await streamWriter.FlushAsync();

        return new EmptyResult();
    }

    private async IAsyncEnumerable<string> GetLogsAsync()
    {
        var logs = await _client.QueryWorkspaceAsync(
            _workspaceId,
            $@"let last_execution_time = toscalar(AppTraces
                | where OperationName == ""DispatchBucketsCrawler"" and Message startswith ""Executing""
                | order by TimeGenerated desc
                | take 1
                | project TimeGenerated);
               AppTraces
                | where TimeGenerated >= last_execution_time // Retrieve only logs from the last execution
                | summarize FirstTimeGenerated=min(TimeGenerated) by OperationId
                | join kind=inner (
                    AppTraces
                    | project TimeGenerated, Message, OperationId, OperationName, Properties
                ) on OperationId
                | join kind=leftouter (
                    AppExceptions
                    | extend Message = tostring(Properties.FormattedMessage)
                ) on OperationId, Message
                | where OperationName in (""{nameof(DispatchBucketsCrawler)}"", ""{nameof(BucketCrawler)}"") // Retrieve only logs about the indexing functions
                    //and Properties.Category !in (""Function.BucketCrawler"") // Ignore logs system logs for BucketCrawler function
                | sort by FirstTimeGenerated asc, OperationId, TimeGenerated asc
                | project TimeGenerated, Message, Error = OuterMessage, OperationId",
            new QueryTimeRange(LogsTimeRange));

        string? lastOperationId = logs.Value.Table.Rows.FirstOrDefault()?.GetString("OperationId");
        foreach (var row in logs.Value.Table.Rows)
        {
            var currentOperationId = row.GetString("OperationId");
            if (lastOperationId != currentOperationId)
            {
                lastOperationId = currentOperationId;
                yield return Environment.NewLine;
            }

            var timestamp = row.GetDateTimeOffset("TimeGenerated");
            var message = row.GetString("Message");

            yield return $"{timestamp:u}: {message}";

            var error = row.GetString("Error");
            if (!string.IsNullOrEmpty(error))
            {
                yield return $"{timestamp:u}: ERROR => {error}";
            }
        }
    }
}
