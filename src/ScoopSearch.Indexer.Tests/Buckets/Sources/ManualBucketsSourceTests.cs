using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ScoopSearch.Indexer.Buckets;
using ScoopSearch.Indexer.Buckets.Providers;
using ScoopSearch.Indexer.Buckets.Sources;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.Tests.Helpers;

namespace ScoopSearch.Indexer.Tests.Buckets.Sources;

public class ManualBucketsSourceTests
{
    private readonly Mock<IBucketsProvider> _bucketsProviderMock;
    private readonly BucketsOptions _bucketsOptions;
    private readonly XUnitLogger<ManualBucketsSource> _logger;
    private readonly ManualBucketsSource _sut;

    public ManualBucketsSourceTests(ITestOutputHelper testOutputHelper)
    {
        _bucketsProviderMock = new Mock<IBucketsProvider>();
        _bucketsOptions = new BucketsOptions();
        _logger = new XUnitLogger<ManualBucketsSource>(testOutputHelper);

        _sut = new ManualBucketsSource(
            new[] {_bucketsProviderMock.Object },
            new OptionsWrapper<BucketsOptions>(_bucketsOptions),
            _logger);
    }

    [Fact]
    public async Task GetBucketsAsync_EmptyManualBuckets_ReturnsEmpty()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        _bucketsOptions.ManualBuckets = null;

        // Act
        var result = await _sut.GetBucketsAsync(cancellationToken).ToArrayAsync(cancellationToken);

        // Arrange
        result.Should().BeEmpty();
        _logger.Should().Log(LogLevel.Warning, "No manual buckets found in configuration");
    }

    [Fact]
    public async Task GetBucketsAsync_ReturnsBuckets()
    {
        // Arrange
        _bucketsOptions.ManualBuckets = new[] { Faker.CreateUri(), Faker.CreateUri() };
        var cancellationToken = new CancellationToken();
        _bucketsProviderMock.Setup(x => x.IsCompatible(_bucketsOptions.ManualBuckets[0])).Returns(false);
        _bucketsProviderMock.Setup(x => x.IsCompatible(_bucketsOptions.ManualBuckets[1])).Returns(true);
        _bucketsProviderMock.Setup(x => x.GetBucketAsync(_bucketsOptions.ManualBuckets[0], cancellationToken)).ReturnsAsync(new Bucket(_bucketsOptions.ManualBuckets[0], 123));
        _bucketsProviderMock.Setup(x => x.GetBucketAsync(_bucketsOptions.ManualBuckets[1], cancellationToken)).ReturnsAsync(new Bucket(_bucketsOptions.ManualBuckets[1], 123));

        // Act
        var result = await _sut.GetBucketsAsync(cancellationToken).ToArrayAsync(cancellationToken);

        // Assert
        result.Should().BeEquivalentTo(new[] { new Bucket(_bucketsOptions.ManualBuckets[1], 123) });
    }
}
