using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.GitHub;
using ScoopSearch.Indexer.Processor;
using ScoopSearch.Indexer.Tests.Helpers;
using Xunit.Abstractions;

namespace ScoopSearch.Indexer.Tests.Processor;

public class FetchBucketsProcessorTests : IClassFixture<HostFixture>
{
    private readonly XUnitLogger<FetchBucketsProcessor> _logger;
    private readonly FetchBucketsProcessor _sut;

    public FetchBucketsProcessorTests(HostFixture hostFixture, ITestOutputHelper testOutputHelper)
    {
        hostFixture.Configure(testOutputHelper);

        _logger = new XUnitLogger<FetchBucketsProcessor>(testOutputHelper);
        _sut = new FetchBucketsProcessor(
            hostFixture.Instance.Services.GetRequiredService<IGitHubClient>(),
            hostFixture.Instance.Services.GetRequiredService<IOptions<BucketsOptions>>(),
            _logger);
    }

    [Fact]
    public async void FetchBucketsAsync_ReturnsBuckets_Succeeds()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        var expectedOfficialBucketsCount = 10;
        var expectedAtLeastBucketsCount = 1400;

        // Act
        var result = await _sut.FetchBucketsAsync(cancellationToken);

        // Assert
        result.Should().HaveCountGreaterOrEqualTo(expectedAtLeastBucketsCount);

        _logger.Should().Log(LogLevel.Information, "Retrieving buckets from sources");
        _logger.Should().Log(LogLevel.Information, _ => Regex.IsMatch(_, $"Found {expectedOfficialBucketsCount} official buckets.+"));
        _logger.Should().Log(LogLevel.Information, _ => Regex.IsMatch(_, @"Found \d{4} buckets on GitHub"));
        _logger.Should().Log(LogLevel.Information, _ => Regex.IsMatch(_, @"Found \d+ buckets to ignore \(appsettings\.json\)"));
        _logger.Should().Log(LogLevel.Information, _ => Regex.IsMatch(_, @"Found \d+ buckets to ignore from external list.+"));
        _logger.Should().Log(LogLevel.Information, _ => Regex.IsMatch(_, @"Found \d+ buckets to add \(appsettings\.json\)"));
        _logger.Should().Log(LogLevel.Information, _ => Regex.IsMatch(_, @"Found \d+ buckets to add from external list.+"));
        _logger.Should().Log(LogLevel.Debug, _ => _.StartsWith("Adding bucket"), Times.AtLeast(expectedAtLeastBucketsCount));
        _logger.Should().Log(LogLevel.Debug, _ => _.StartsWith("Adding bucket 'https://github.com/ScoopInstaller/Main'"));
        _logger.Should().Log(LogLevel.Debug, _ => _.StartsWith("Adding bucket 'https://github.com/ScoopInstaller/Extras'"));
    }
}
