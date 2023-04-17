using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ScoopSearch.Functions.Configuration;
using ScoopSearch.Functions.Function.Indexing;
using ScoopSearch.Functions.GitHub;
using ScoopSearch.Functions.Indexer;
using ScoopSearch.Functions.Tests.Helpers;
using Xunit.Abstractions;

namespace ScoopSearch.Functions.Tests.Function.Indexing;

public class FetchBucketsActivityTests : IClassFixture<HostFixture>
{
    private readonly XUnitLogger<FetchBucketsActivity> _logger;
    private readonly Mock<IIndexer> _indexerMock;
    private readonly FetchBucketsActivity _sut;

    public FetchBucketsActivityTests(HostFixture hostFixture, ITestOutputHelper testOutputHelper)
    {
        hostFixture.Configure(testOutputHelper);

        _logger = new XUnitLogger<FetchBucketsActivity>(testOutputHelper);
        _indexerMock = new Mock<IIndexer>();
        _sut = new FetchBucketsActivity(
            hostFixture.Host.Services.GetRequiredService<IGitHubClient>(),
            _indexerMock.Object,
            hostFixture.Host.Services.GetRequiredService<IOptions<BucketsOptions>>());
    }

    [Fact]
    public async void FetchBuckets_ManifestMetadataUpdatedFromBucket_Succeeds()
    {
        // Arrange
        var durableActivityContext = Mock.Of<IDurableActivityContext>();
        var cancellationToken = CancellationToken.None;
        var expectedOfficialBucketsCount = 10;
        var expectedAtLeastBucketsCount = 1400;

        // Act
        var result = await _sut.FetchBuckets(durableActivityContext, _logger);

        // Assert
        _indexerMock.Verify(_ => _.GetBucketsAsync(cancellationToken));
        _indexerMock.VerifyNoOtherCalls();;
        result.Should().HaveCountGreaterOrEqualTo(expectedAtLeastBucketsCount);

        _logger.Should().Log(LogLevel.Information, "Retrieving buckets from sources");
        _logger.Should().Log(LogLevel.Information, _ => Regex.IsMatch(_, $"Found {expectedOfficialBucketsCount} official buckets.+"));
        _logger.Should().Log(LogLevel.Information, _ => Regex.IsMatch(_, @"Found \d{4} buckets on GitHub\."));
        _logger.Should().Log(LogLevel.Information, _ => Regex.IsMatch(_, @"Found \d+ buckets to ignore \(settings\.json\)\."));
        _logger.Should().Log(LogLevel.Information, _ => Regex.IsMatch(_, @"Found \d+ buckets to ignore from external list.+"));
        _logger.Should().Log(LogLevel.Information, _ => Regex.IsMatch(_, @"Found \d+ buckets to add \(settings\.json\)\."));
        _logger.Should().Log(LogLevel.Information, _ => Regex.IsMatch(_, @"Found \d+ buckets to add from external list.+"));
        _logger.Should().Log(LogLevel.Information, _ => Regex.IsMatch(_, @"\d+ buckets to remove from the index\."));
        _logger.Should().Log(LogLevel.Information, _ => Regex.IsMatch(_, @"Adding \d{4} buckets for indexing\."));
        _logger.Should().Log(LogLevel.Debug, _ => _.StartsWith("Adding bucket"), Times.AtLeast(expectedAtLeastBucketsCount));
        _logger.Should().Log(LogLevel.Debug, _ => _.StartsWith("Adding bucket 'https://github.com/ScoopInstaller/Main'"));
        _logger.Should().Log(LogLevel.Debug, _ => _.StartsWith("Adding bucket 'https://github.com/ScoopInstaller/Extras'"));
    }
}
