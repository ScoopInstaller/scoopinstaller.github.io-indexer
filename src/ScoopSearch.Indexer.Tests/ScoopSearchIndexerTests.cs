using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ScoopSearch.Indexer.Buckets;
using ScoopSearch.Indexer.Buckets.Sources;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.Processor;
using ScoopSearch.Indexer.Tests.Helpers;
using Xunit.Abstractions;

namespace ScoopSearch.Indexer.Tests;

public class ScoopSearchIndexerTests : IClassFixture<HostFixture>
{
    private readonly Mock<IIndexingProcessor> _indexingProcessorMock;
    private readonly XUnitLogger<ScoopSearchIndexer> _logger;
    private readonly ScoopSearchIndexer _sut;

    public ScoopSearchIndexerTests(HostFixture hostFixture, ITestOutputHelper testOutputHelper)
    {
        hostFixture.Configure(testOutputHelper);

        _indexingProcessorMock = new Mock<IIndexingProcessor>();

        var fetchManifestsProcessorMock = new Mock<IFetchManifestsProcessor>();
        fetchManifestsProcessorMock
            .Setup(x => x.FetchManifestsAsync(It.IsAny<Bucket>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<ManifestInfo>());

        _logger = new XUnitLogger<ScoopSearchIndexer>(testOutputHelper);
        _sut = new ScoopSearchIndexer(
            hostFixture.Instance.Services.GetRequiredService<IEnumerable<IBucketsSource>>(),
            hostFixture.Instance.Services.GetRequiredService<IOfficialBucketsSource>(),
            fetchManifestsProcessorMock.Object,
            _indexingProcessorMock.Object,
            hostFixture.Instance.Services.GetRequiredService<IOptions<BucketsOptions>>(),
            _logger);
    }

    [Fact]
    public async void ExecuteAsync_ReturnsBuckets_Succeeds()
    {
        // Arrange
        const int expectedAtLeastBucketsCount = 1500;
        var cancellationToken = new CancellationToken();
        Uri[]? actualBucketsUris = null;
        _indexingProcessorMock
            .Setup(x => x.CleanIndexFromNonExistentBucketsAsync(It.IsAny<Uri[]>(), cancellationToken))
            .Returns(Task.CompletedTask)
            .Callback<Uri[], CancellationToken>((uris, _) => actualBucketsUris = uris)
            .Verifiable();

        // Act
        await _sut.ExecuteAsync(cancellationToken);

        // Assert
        _indexingProcessorMock.Verify();
        actualBucketsUris.Should().HaveCountGreaterThan(expectedAtLeastBucketsCount);
        actualBucketsUris.Should().OnlyHaveUniqueItems(_ => _.AbsoluteUri.ToLowerInvariant());

        _logger.Should().Log(LogLevel.Information, _ => Regex.IsMatch(_, @"Found \d+ buckets for a total of \d+ manifests."));
        _logger.Should().Log(LogLevel.Information, _ => _.StartsWith("Processed bucket "), Times.AtLeast(expectedAtLeastBucketsCount));
        _logger.Should().Log(LogLevel.Information, _ => _.StartsWith("Processed bucket https://github.com/ScoopInstaller/Main"));
        _logger.Should().Log(LogLevel.Information, _ => _.StartsWith("Processed bucket https://github.com/ScoopInstaller/Extras"));
    }
}
