using Azure;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ScoopSearch.Functions.Configuration;
using ScoopSearch.Functions.Function;

namespace ScoopSearch.Functions.Tests.Function;

public class IndexingLogTests : IClassFixture<HostFixture>
{
    private readonly IndexingLog _sut;
    private readonly Mock<IOptions<AzureLogsMonitorOptions>> _options;
    private readonly Mock<LogsQueryClient> _logsQueryClientMock;

    public IndexingLogTests()
    {
        _options = new Mock<IOptions<AzureLogsMonitorOptions>>();
        _options.Setup(_ => _.Value).Returns(new AzureLogsMonitorOptions() { WorkspaceId = "workspaceId", TenantId = "tenantId", ClientId = "clientId", ClientSecret = "clientSecret" });
        _logsQueryClientMock = new Mock<LogsQueryClient>();
        _sut = new IndexingLog(_options.Object, _ =>
        {
            _.Should().BeOfType<ClientSecretCredential>();
            return _logsQueryClientMock.Object;
        });
    }

    [Fact]
    public async void Run_InvalidOptions_Throws()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var request = new DefaultHttpRequest(context);
        _logsQueryClientMock
            .Setup(_ => _.QueryWorkspaceAsync(_options.Object.Value.WorkspaceId, It.IsAny<string>(), It.Is<QueryTimeRange>(_ => _.Duration == TimeSpan.FromHours(24)), It.IsAny<LogsQueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLogsQueryResult());

        // Act
        var result = await _sut.Run(request, Mock.Of<ILogger>());

        // Assert
        result.Should().BeOfType<EmptyResult>();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("text/plain");
        context.Response.Headers.Should().ContainSingle(_ => _.Key == "Cache-Control" && _.Value == "public, no-store, max-age=60");
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var streamReader = new StreamReader(context.Response.Body);
        streamReader.ReadToEnd().Should().Be($"2021-08-01 00:00:00Z: Message1{Environment.NewLine}" +
                                             $"2021-08-01 00:00:01Z: Message2{Environment.NewLine}" +
                                             $"2021-08-01 00:00:01Z: ERROR => Error{Environment.NewLine}" +
                                             Environment.NewLine +
                                             Environment.NewLine +
                                             $"2021-08-01 00:00:02Z: Message3{Environment.NewLine}"
        );
    }

    private Response<LogsQueryResult> CreateLogsQueryResult()
    {
        var tableColumns = new[]
        {
            MonitorQueryModelFactory.LogsTableColumn("TimeGenerated", LogsColumnType.Datetime),
            MonitorQueryModelFactory.LogsTableColumn("Message", LogsColumnType.String),
            MonitorQueryModelFactory.LogsTableColumn("Error", LogsColumnType.String),
            MonitorQueryModelFactory.LogsTableColumn("OperationId", LogsColumnType.String)
        };

        var tableRows = new[]
        {
            MonitorQueryModelFactory.LogsTableRow(tableColumns, new object[]{ "2021-08-01T00:00:00Z", "Message1", "", "OperationId1" }),
            MonitorQueryModelFactory.LogsTableRow(tableColumns, new object[]{ "2021-08-01T00:00:01Z", "Message2", "Error", "OperationId1" }),
            MonitorQueryModelFactory.LogsTableRow(tableColumns, new object[]{ "2021-08-01T00:00:02Z", "Message3", "", "OperationId2" })
        };

        var table = MonitorQueryModelFactory.LogsTable("Table", tableColumns, tableRows);

        return Response.FromValue(
            MonitorQueryModelFactory.LogsQueryResult(
                new[] { table },
                new BinaryData("{}"),
                new BinaryData("{}"),
                new BinaryData("{}")),
            Mock.Of<Response>());
    }
}
