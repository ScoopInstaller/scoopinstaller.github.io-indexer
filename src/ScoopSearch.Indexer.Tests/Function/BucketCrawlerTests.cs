using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.Function;
using ScoopSearch.Indexer.Indexer;
using ScoopSearch.Indexer.Manifest;
using ScoopSearch.Indexer.Tests.Helpers;
using Xunit.Abstractions;

namespace ScoopSearch.Indexer.Tests.Function;

public class BucketCrawlerTests : IClassFixture<HostFixture>
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly XUnitLogger<BucketCrawler> _logger;
    private readonly Mock<IIndexer> _indexerMock;
    private readonly BucketCrawler _sut;

    public BucketCrawlerTests(HostFixture hostFixture, ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        hostFixture.Configure(testOutputHelper);

        _logger = new XUnitLogger<BucketCrawler>(testOutputHelper);
        _indexerMock = new Mock<IIndexer>();
        _sut = new BucketCrawler(hostFixture.Host.Services.GetRequiredService<IManifestCrawler>(), _indexerMock.Object);
    }

    [Theory]
    [CombinatorialData]
    public async void Run_ManifestMetadataUpdatedFromBucket_Succeeds([CombinatorialValues(123, 321)] int stars, bool officialRepository)
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var queueItem = new QueueItem(uri, stars, officialRepository);
        var cancellationToken = new CancellationToken();

        // Act
        await _sut.Run(queueItem, _logger, cancellationToken);

        // Assert
        _indexerMock.Verify(_ => _.AddManifestsAsync(FluentExtensions.Matcher<IEnumerable<ManifestInfo>>(_ => _
            .Should().HaveCount(5, "")
            .And.Contain(_ => _.Metadata.RepositoryStars == stars && _.Metadata.OfficialRepository == officialRepository, ""),
            _testOutputHelper), cancellationToken));
    }

    [Theory]
    [InlineData(Constants.NonExistentTestRepositoryUri)]
    [InlineData(Constants.EmptyTestRepositoryUri)]
    public async void Run_InvalidRepository_Succeeds(string repository)
    {
        // Arrange
        var uri = new Uri(repository);
        var queueItem = new QueueItem(uri, 0, false);
        var cancellationToken = new CancellationToken();
        _indexerMock
            .Setup(_ => _.GetExistingManifestsAsync(uri, cancellationToken))
            .Returns(Task.FromResult<IEnumerable<ManifestInfo>>(new [] { ("foo", "bar", 0).ToManifestInfo() } ))
            .Verifiable();

        // Act
        await _sut.Run(queueItem, _logger, cancellationToken);

        // Assert
        _logger.Should().Log(LogLevel.Information, $"Found 0 manifests for {uri}")
            .And.Log(LogLevel.Information, $"1 existing manifests. 0 manifests to add / 1 manifests to remove / 0 manifests to update");
        _indexerMock.Verify(_ => _.DeleteManifestsAsync(BuildMatcher(new[] { new { Id = "foo" } }), cancellationToken));
        _indexerMock.Verify();
        _indexerMock.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(GetRunTestCases))]
    public async void Run_ExistingRepository_RemoveNonExistentManifest(
        ManifestInfo[] index,
        int expectedManifestsToAddCount,
        int expectedManifestsToUpdateCount,
        object[] expectedManifestsToAddOrUpdate,
        object[] expectedManifestsToRemove)
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var cancellationToken = new CancellationToken();
        _indexerMock
            .Setup(_ => _.GetExistingManifestsAsync(uri, cancellationToken))
            .Returns(Task.FromResult<IEnumerable<ManifestInfo>>(index))
            .Verifiable();

        // Act
        await _sut.Run(new QueueItem(uri, 123, false), _logger, cancellationToken);

        // Assert
        _logger.Should().Log(LogLevel.Information, $"Found 5 manifests for {uri}")
            .And.Log(LogLevel.Information, $"{index.Length} existing manifests. {expectedManifestsToAddCount} manifests to add / {expectedManifestsToRemove.Length} manifests to remove / {expectedManifestsToUpdateCount} manifests to update");

        if (expectedManifestsToAddOrUpdate.Any())
        {
            _indexerMock.Verify(_ => _.AddManifestsAsync(BuildMatcher(expectedManifestsToAddOrUpdate), cancellationToken));
        }

        if (expectedManifestsToRemove.Any())
        {
            _indexerMock.Verify(_ => _.DeleteManifestsAsync(BuildMatcher(expectedManifestsToRemove), cancellationToken));
        }

        _indexerMock.Verify();
        _indexerMock.VerifyNoOtherCalls();
    }

    public static IEnumerable<object[]> GetRunTestCases()
    {
        ManifestInfo[] manifestsInIndex;
        object[] expectedManifestsToAddOrUpdate;
        object[] expectedManifestsToRemove;

        // Add all manifests to the index
        manifestsInIndex = Array.Empty<ManifestInfo>();
        expectedManifestsToAddOrUpdate = new[]
        {
            new { Name = "cdex" },
            new { Name = "cfg-cam" },
            new { Name = "dnsproxy" },
            new { Name = "kaxaml" },
            new { Name = "OpenHashTab" },
        };
        expectedManifestsToRemove = Array.Empty<object>();
        yield return new object[] { manifestsInIndex, 5, 0, expectedManifestsToAddOrUpdate, expectedManifestsToRemove };

        // All manifests already in the index and up to date
        manifestsInIndex = new[]
        {
            ("19acee9283bf6185ff60a0960cc4b196b185b688", "0c91acbccc09ddac3bcce55970ff9c4f811012ec", 123).ToManifestInfo(), // cdex
            ("6aeea6969109db70a61a61ca1d851acddf03aa34", "f6d6b8ac0ddd2dd66a4610421c9d526a55bcbe6c", 123).ToManifestInfo(), // cfg-cam
            ("2f0fe5924b8f879f9944fb78d79c63f6a970a81d", "83d1afa833e572c55a3954dfe8cd1d2b4e1e749b", 123).ToManifestInfo(), // dnsproxy
            ("b6b519ecf87a20fffdd1d799aaf78eec4817a8d1", "c9741fe372820c045ade167f6fc7727e384e6238", 123).ToManifestInfo(), // kaxaml
            ("e5b51e849f4bfac169a66ec43ef452d4a9902d26", "35f8371051986f6a89017bb901e44e1deaf06111", 123).ToManifestInfo(), // OpenHashTab
        };
        expectedManifestsToAddOrUpdate = Array.Empty<object>();
        expectedManifestsToRemove = Array.Empty<object>();
        yield return new object[] { manifestsInIndex, 0, 0, expectedManifestsToAddOrUpdate, expectedManifestsToRemove };

        // All manifests already in the index and not all are up to date
        manifestsInIndex = new[]
        {
            ("19acee9283bf6185ff60a0960cc4b196b185b688", "0c91acbccc09ddac3bcce55970ff9c4f811012ec", 123).ToManifestInfo(), // cdex
            ("6aeea6969109db70a61a61ca1d851acddf03aa34", "f6d6b8ac0ddd2dd66a4610421c9d526a55bcbe6c", 123).ToManifestInfo(), // cfg-cam
            ("2f0fe5924b8f879f9944fb78d79c63f6a970a81d", "83d1afa833e572c55a3954dfe8cd1d2b4e1e749b", 0).ToManifestInfo(), // dnsproxy
            ("b6b519ecf87a20fffdd1d799aaf78eec4817a8d1", "foo", 123).ToManifestInfo(), // kaxaml
            ("e5b51e849f4bfac169a66ec43ef452d4a9902d26", "35f8371051986f6a89017bb901e44e1deaf06111", 0).ToManifestInfo(), // OpenHashTab
        };
        expectedManifestsToAddOrUpdate = new[]
        {
            new { Name = "dnsproxy" },
            new { Name = "kaxaml" },
            new { Name = "OpenHashTab" },
        };
        expectedManifestsToRemove = Array.Empty<object>();
        yield return new object[] { manifestsInIndex, 0, 3, expectedManifestsToAddOrUpdate, expectedManifestsToRemove };

        // Some manifests in the index but not on the repo anymore
        manifestsInIndex = new[]
        {
            ("19acee9283bf6185ff60a0960cc4b196b185b688", "0c91acbccc09ddac3bcce55970ff9c4f811012ec", 123).ToManifestInfo(), // cdex
            ("6aeea6969109db70a61a61ca1d851acddf03aa34", "f6d6b8ac0ddd2dd66a4610421c9d526a55bcbe6c", 123).ToManifestInfo(), // cfg-cam
            ("2f0fe5924b8f879f9944fb78d79c63f6a970a81d", "83d1afa833e572c55a3954dfe8cd1d2b4e1e749b", 123).ToManifestInfo(), // dnsproxy
            ("b6b519ecf87a20fffdd1d799aaf78eec4817a8d1", "c9741fe372820c045ade167f6fc7727e384e6238", 123).ToManifestInfo(), // kaxaml
            ("e5b51e849f4bfac169a66ec43ef452d4a9902d26", "35f8371051986f6a89017bb901e44e1deaf06111", 123).ToManifestInfo(), // OpenHashTab
            ("foo", "bar", 123).ToManifestInfo(), // random
        };
        expectedManifestsToAddOrUpdate = Array.Empty<object>();
        expectedManifestsToRemove = new[]
        {
            new { Id = "foo" }, // random
        };
        yield return new object[] { manifestsInIndex, 0, 0, expectedManifestsToAddOrUpdate, expectedManifestsToRemove };

        // Mix everything
        manifestsInIndex = new[]
        {
            ("19acee9283bf6185ff60a0960cc4b196b185b688", "0c91acbccc09ddac3bcce55970ff9c4f811012ec", 123).ToManifestInfo(), // cdex - original
            ("6aeea6969109db70a61a61ca1d851acddf03aa34", "f6d6b8ac0ddd2dd66a4610421c9d526a55bcbe6c", 0).ToManifestInfo(), // cfg-cam - stars changed
            ("2f0fe5924b8f879f9944fb78d79c63f6a970a81d", "foo", 123).ToManifestInfo(), // dnsproxy - sha changed
            ("foo", "bar", 123).ToManifestInfo(), // random - not on the repo anymore
        };
        expectedManifestsToAddOrUpdate = new[]
        {
            new { Name = "cfg-cam" },
            new { Name = "dnsproxy" },
            new { Name = "kaxaml" },
            new { Name = "OpenHashTab" },
        };
        expectedManifestsToRemove = new[]
        {
            new { Id = "foo" },
        };
        yield return new object[] { manifestsInIndex, 2, 2, expectedManifestsToAddOrUpdate, expectedManifestsToRemove };
    }

    private IEnumerable<ManifestInfo> BuildMatcher(object[] expected)
    {
        return FluentExtensions.Matcher<IEnumerable<ManifestInfo>>(_ =>
            _.Should().BeEquivalentTo(expected, options => options.ExcludingMissingMembers(), ""), _testOutputHelper);
    }
}
