using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    private static readonly TimeSpan DefaultLogsRange = TimeSpan.FromHours(6);
    private static readonly TimeSpan MaxLogsRange = TimeSpan.FromDays(2);

    private readonly LogsQueryClient _client;
    private readonly string _workspaceId;

    public IndexingLog(IOptions<AzureLogsMonitorOptions> options)
    {
        _workspaceId = options.Value.WorkspaceId;

        var token = new ClientSecretCredential(
            tenantId: options.Value.TenantId,
            clientId: options.Value.ClientId,
            clientSecret: options.Value.ClientSecret);

        _client = new LogsQueryClient(token);
    }

    [FunctionName("IndexingLog")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]
        HttpRequest request,
        ILogger logger)
    {
        var logsRange = GetLogsRange(request.Query);

        var response = request.HttpContext.Response;
        response.StatusCode = 200;
        response.ContentType = "text/plain";
        await using var streamWriter = new StreamWriter(response.Body);

        await foreach (var row in GetLogsAsync(logsRange))
        {
            await streamWriter.WriteLineAsync(row);
        }

        await streamWriter.FlushAsync();

        return new EmptyResult();
    }

    private TimeSpan GetLogsRange(IQueryCollection requestQuery)
    {

        if (requestQuery.TryGetValue("logs_range", out var value)
            && int.TryParse(value, out var logsRangeHours)
            && logsRangeHours > 0
            && logsRangeHours <= MaxLogsRange.TotalHours)
        {
            return TimeSpan.FromHours(logsRangeHours);
        }

        return DefaultLogsRange;
    }

    private async IAsyncEnumerable<string> GetLogsAsync(TimeSpan logsRange)
    {
        var timeRange = new QueryTimeRange(logsRange);

        var tracesLogsTask = _client.QueryWorkspaceAsync(
            _workspaceId,
            @"AppTraces
                    | where OperationName != """"
                    | where SeverityLevel != 3 // skip errors because we aggregate them below
                    | project Timestamp = TimeGenerated, Message = Message, OperationId = OperationId",
            timeRange);

        var exceptionsLogsTask = _client.QueryWorkspaceAsync(
            _workspaceId,
            @"AppExceptions
                    | project Timestamp = TimeGenerated, Message = Properties.FormattedMessage, Error = OuterMessage, OperationId = OperationId",
            timeRange);

        await Task.WhenAll(tracesLogsTask, exceptionsLogsTask);
        var tracesTable = tracesLogsTask.Result.Value.Table;
        var exceptionsTable = exceptionsLogsTask.Result.Value.Table;

        var tracesGroups = tracesTable.Rows.GroupBy(_ => _["OperationId"]).OrderBy(_ => _.First()["Timestamp"]);
        var exceptionsGroups = exceptionsTable.Rows.GroupBy(_ => _["OperationId"]);

        foreach (var tracesGroup in tracesGroups)
        {
            var messages = new List<(DateTimeOffset Timestamp, string Text)>();
            tracesGroup.ForEach(_ => messages.Add((_.GetDateTimeOffset("Timestamp")!.Value, _.GetString("Message"))));

            var exceptions = exceptionsGroups.SingleOrDefault(_ => _.Key.Equals(tracesGroup.Key));
            exceptions?.ForEach(_ => messages.Add((_.GetDateTimeOffset("Timestamp")!.Value, $"{_.GetString("Message")} (details: {_.GetString("Error")})")));

            foreach (var message in messages.OrderBy(_ => _.Timestamp))
            {
                yield return $"{message.Timestamp:u}: {message.Text}";
            }

            yield return Environment.NewLine;
        }
    }
}
