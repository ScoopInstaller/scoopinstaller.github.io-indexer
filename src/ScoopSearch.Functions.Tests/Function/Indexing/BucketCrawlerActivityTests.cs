using Bogus;
using FluentAssertions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ScoopSearch.Functions.Data;
using ScoopSearch.Functions.Function.Indexing;
using ScoopSearch.Functions.Indexer;
using ScoopSearch.Functions.Manifest;
using ScoopSearch.Functions.Tests.Helpers;
using Xunit.Abstractions;
using Faker = ScoopSearch.Functions.Tests.Helpers.Faker;

namespace ScoopSearch.Functions.Tests.Function.Indexing;

public class BucketCrawlerActivityTests : IClassFixture<HostFixture>
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly XUnitLogger<BucketCrawlerActivity> _logger;
    private readonly Mock<IIndexer> _indexerMock;
    private readonly BucketCrawlerActivity _sut;

    public BucketCrawlerActivityTests(HostFixture hostFixture, ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        hostFixture.Configure(testOutputHelper);

        _logger = new XUnitLogger<BucketCrawlerActivity>(testOutputHelper);
        _indexerMock = new Mock<IIndexer>();
        _sut = new BucketCrawlerActivity(hostFixture.Host.Services.GetRequiredService<IManifestCrawler>(), _indexerMock.Object);
    }

    [Theory]
    [CombinatorialData]
    public async void CrawlBucket_ValidRepository_ManifestsAddedToIndex([CombinatorialValues(123, 321)] int stars, bool officialRepository)
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var bucketInfo = new BucketInfo(uri, stars, officialRepository);
        var durableActivityContext = Mock.Of<IDurableActivityContext>(_ => _.GetInput<BucketInfo>() == bucketInfo);
        var cancellationToken = CancellationToken.None;

        // Act
        await _sut.CrawlBucket(durableActivityContext, _logger);

        // Assert
        _indexerMock.Verify(_ => _.AddManifestsAsync(FluentExtensions.Matcher<IEnumerable<ManifestInfo>>(_ => _
            .Should().HaveCount(5, "")
            .And.Contain(_ => _.Metadata.RepositoryStars == stars && _.Metadata.OfficialRepository == officialRepository, ""),
            _testOutputHelper), cancellationToken));
    }

    [Theory]
    [InlineData(Constants.NonExistentTestRepositoryUri)]
    [InlineData(Constants.EmptyTestRepositoryUri)]
    public async void CrawlBucket_InvalidRepository_ManifestsRemovedFromIndex(string repository)
    {
        // Arrange
        var uri = new Uri(repository);
        var bucketInfo = new BucketInfo(uri, 0, false);
        var durableActivityContext = Mock.Of<IDurableActivityContext>(_ => _.GetInput<BucketInfo>() == bucketInfo);
        var cancellationToken = CancellationToken.None;
        const int existingManifestsInIndex = 3;
        var manifestsInfo = Faker.CreateManifestInfo().Generate(existingManifestsInIndex);
        _indexerMock
            .Setup(_ => _.GetExistingManifestsAsync(uri, cancellationToken))
            .Returns(Task.FromResult<IEnumerable<ManifestInfo>>(manifestsInfo))
            .Verifiable();

        // Act
        await _sut.CrawlBucket(durableActivityContext, _logger);

        // Assert
        _logger.Should().Log(LogLevel.Information, $"Found 0 manifests for {uri}")
            .And.Log(LogLevel.Information, $"{existingManifestsInIndex} existing manifests. 0 manifests to add / {existingManifestsInIndex} manifests to remove / 0 manifests to update");
        _indexerMock.Verify(_ => _.DeleteManifestsAsync(manifestsInfo, cancellationToken));
        _indexerMock.Verify();
        _indexerMock.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(GetRunTestCases))]
    public async void CrawlBucket_ExistingRepository_IndexUpdated(
        ManifestInfo[] index,
        int expectedManifestsToAddCount,
        int expectedManifestsToUpdateCount,
        ManifestInfo[] expectedManifestsToAddOrUpdate,
        ManifestInfo[] expectedManifestsToRemove)
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var bucketInfo = new BucketInfo(uri, 123, false);
        var durableActivityContext = Mock.Of<IDurableActivityContext>(_ => _.GetInput<BucketInfo>() == bucketInfo);
        var cancellationToken = CancellationToken.None;
        _indexerMock
            .Setup(_ => _.GetExistingManifestsAsync(uri, cancellationToken))
            .Returns(Task.FromResult<IEnumerable<ManifestInfo>>(index))
            .Verifiable();

        // Act
        await _sut.CrawlBucket(durableActivityContext, _logger);

        // Assert
        _logger.Should().Log(LogLevel.Information, $"Found 5 manifests for {uri}")
            .And.Log(LogLevel.Information, $"{index.Length} existing manifests. {expectedManifestsToAddCount} manifests to add / {expectedManifestsToRemove.Length} manifests to remove / {expectedManifestsToUpdateCount} manifests to update");

        if (expectedManifestsToAddOrUpdate.Any())
        {
            _indexerMock.Verify(_ => _.AddManifestsAsync(ManifestsMatcher(expectedManifestsToAddOrUpdate), cancellationToken));
        }

        if (expectedManifestsToRemove.Any())
        {
            _indexerMock.Verify(_ => _.DeleteManifestsAsync(ManifestsMatcher(expectedManifestsToRemove), cancellationToken));
        }

        _indexerMock.Verify();
        _indexerMock.VerifyNoOtherCalls();
    }

    public static IEnumerable<object[]> GetRunTestCases()
    {
        ManifestInfo[] manifestsInIndex;
        ManifestInfo[] expectedManifestsToAddOrUpdate;
        ManifestInfo[] expectedManifestsToRemove;
        (Faker<ManifestInfo> ManifestInfo, Faker<ManifestMetadata> ManifestMetadata)[] fakeData;

        // Add all manifests to the index
        fakeData = CreateFakeData().ToArray();
        manifestsInIndex = Array.Empty<ManifestInfo>();
        expectedManifestsToAddOrUpdate = fakeData.Select(_ => _.ManifestInfo.Generate()).ToArray();
        expectedManifestsToRemove = Array.Empty<ManifestInfo>();
        yield return new object[] { manifestsInIndex, 5, 0, expectedManifestsToAddOrUpdate, expectedManifestsToRemove };

        // All manifests already in the index and up to date
        fakeData = CreateFakeData().ToArray();
        manifestsInIndex = fakeData.Select(_ => _.ManifestInfo.Generate()).ToArray();
        expectedManifestsToAddOrUpdate = Array.Empty<ManifestInfo>();
        expectedManifestsToRemove = Array.Empty<ManifestInfo>();
        yield return new object[] { manifestsInIndex, 0, 0, expectedManifestsToAddOrUpdate, expectedManifestsToRemove };

        // All manifests already in the index and not all are up to date
        fakeData = CreateFakeData().ToArray();
        fakeData[1].ManifestMetadata.RuleFor(_ => _.RepositoryStars, 0);
        fakeData[2].ManifestMetadata.RuleFor(_ => _.Sha, f => f.Random.Hash());
        fakeData[3].ManifestMetadata.RuleFor(_ => _.RepositoryStars, 0);
        fakeData[4].ManifestMetadata.RuleFor(_ => _.ManifestHash, f => f.Random.Hash());
        manifestsInIndex = fakeData.Select(_ => _.ManifestInfo.Generate()).ToArray();
        expectedManifestsToAddOrUpdate = new[] { manifestsInIndex[1], manifestsInIndex[2], manifestsInIndex[3], manifestsInIndex[4] };
        expectedManifestsToRemove = Array.Empty<ManifestInfo>();
        yield return new object[] { manifestsInIndex, 0, 4, expectedManifestsToAddOrUpdate, expectedManifestsToRemove };

        // Some manifests in the index but not on the repo anymore
        fakeData = CreateFakeData().ToArray();
        manifestsInIndex = fakeData
            .Select(_ => _.ManifestInfo.Generate())
            .Concat(new[] { Faker.CreateManifestInfo().Generate() })
            .ToArray();
        expectedManifestsToAddOrUpdate = Array.Empty<ManifestInfo>();
        expectedManifestsToRemove = new[] { manifestsInIndex[5] };
        yield return new object[] { manifestsInIndex, 0, 0, expectedManifestsToAddOrUpdate, expectedManifestsToRemove };

        // Mix everything
        fakeData = CreateFakeData().ToArray();
        fakeData[1].ManifestMetadata.RuleFor(_ => _.RepositoryStars, 0);
        fakeData[2].ManifestMetadata.RuleFor(_ => _.Sha, f => f.Random.Hash());
        fakeData[3].ManifestMetadata.RuleFor(_ => _.ManifestHash, f => f.Random.Hash());
        var otherManifestInfo = Faker.CreateManifestInfo().Generate();
        manifestsInIndex = new[]
        {
            fakeData[0].ManifestInfo.Generate(), // Original
            fakeData[1].ManifestInfo.Generate(), // stars changed
            fakeData[2].ManifestInfo.Generate(), // sha changed
            fakeData[3].ManifestInfo.Generate(), // manifest hash changed
            otherManifestInfo // not in the repo anymore
        };
        expectedManifestsToAddOrUpdate = new[]
        {
            fakeData[1].ManifestInfo.Generate(),
            fakeData[2].ManifestInfo.Generate(),
            fakeData[3].ManifestInfo.Generate(),
            fakeData[4].ManifestInfo.Generate()
        };
        expectedManifestsToRemove = new[]
        {
            otherManifestInfo
        };
        yield return new object[] { manifestsInIndex, 1, 3, expectedManifestsToAddOrUpdate, expectedManifestsToRemove };
    }

    private static IEnumerable<(Faker<ManifestInfo> ManifestInfo, Faker<ManifestMetadata> ManifestMetadata)> CreateFakeData()
    {
        var cdexMetadata = Faker.CreateManifestMetadata()
            .RuleFor(_ => _.Sha, "0c91acbccc09ddac3bcce55970ff9c4f811012ec")
            .RuleFor(_ => _.RepositoryStars, 123)
            .RuleFor(_ => _.OfficialRepositoryNumber, 0)
            .RuleFor(_ => _.ManifestHash, "a5e9ab1b84239cae4747eb8d517248ee503a7809");
        var cdexManifestInfo = Faker.CreateManifestInfo()
            .RuleFor(_ => _.Id, "19acee9283bf6185ff60a0960cc4b196b185b688")
            .RuleFor(_ => _.Name, "cdex")
            .RuleFor(_ => _.Metadata, () => cdexMetadata);
        yield return (cdexManifestInfo, cdexMetadata);

        var cfgCamMetadata = Faker.CreateManifestMetadata()
            .RuleFor(_ => _.Sha, "f6d6b8ac0ddd2dd66a4610421c9d526a55bcbe6c")
            .RuleFor(_ => _.RepositoryStars, 123)
            .RuleFor(_ => _.OfficialRepositoryNumber, 0)
            .RuleFor(_ => _.ManifestHash, "5ca0b753c198e9586d6da0f3b6732a24a86c0389");
        var cfgCamManifestInfo = Faker.CreateManifestInfo()
            .RuleFor(_ => _.Id, "6aeea6969109db70a61a61ca1d851acddf03aa34")
            .RuleFor(_ => _.Name, "cdg-cam")
            .RuleFor(_ => _.Metadata, () => cfgCamMetadata);
        yield return (cfgCamManifestInfo, cfgCamMetadata);

        var dnsProxyMetadata = Faker.CreateManifestMetadata()
            .RuleFor(_ => _.Sha, "83d1afa833e572c55a3954dfe8cd1d2b4e1e749b")
            .RuleFor(_ => _.RepositoryStars, 123)
            .RuleFor(_ => _.OfficialRepositoryNumber, 0)
            .RuleFor(_ => _.ManifestHash, "b1e247c22c8c8a219a031776f1489776d1b53976");
        var dnsProxyManifestInfo = Faker.CreateManifestInfo()
            .RuleFor(_ => _.Id, "2f0fe5924b8f879f9944fb78d79c63f6a970a81d")
            .RuleFor(_ => _.Name, "dnsproxy")
            .RuleFor(_ => _.Metadata, () => dnsProxyMetadata);
        yield return (dnsProxyManifestInfo, dnsProxyMetadata);

        var kaxamlMetadata = Faker.CreateManifestMetadata()
            .RuleFor(_ => _.Sha, "c9741fe372820c045ade167f6fc7727e384e6238")
            .RuleFor(_ => _.RepositoryStars, 123)
            .RuleFor(_ => _.OfficialRepositoryNumber, 0)
            .RuleFor(_ => _.ManifestHash, "71c72b06e4466f794580529b4e45ed7e2fee8f95");
        var kaxamlManifestInfo = Faker.CreateManifestInfo()
            .RuleFor(_ => _.Id, "b6b519ecf87a20fffdd1d799aaf78eec4817a8d1")
            .RuleFor(_ => _.Name, "kaxaml")
            .RuleFor(_ => _.Metadata, () => kaxamlMetadata);
        yield return (kaxamlManifestInfo, kaxamlMetadata);

        var openHashTabMetadata = Faker.CreateManifestMetadata()
            .RuleFor(_ => _.Sha, "35f8371051986f6a89017bb901e44e1deaf06111")
            .RuleFor(_ => _.RepositoryStars, 123)
            .RuleFor(_ => _.OfficialRepositoryNumber, 0)
            .RuleFor(_ => _.ManifestHash, "71dfe99539b8f16f8da3cba47855a5ff0ea9c359");
        var openHashTabManifestInfo = Faker.CreateManifestInfo()
            .RuleFor(_ => _.Id, "e5b51e849f4bfac169a66ec43ef452d4a9902d26")
            .RuleFor(_ => _.Name, "OpenHashTab")
            .RuleFor(_ => _.Metadata, () => openHashTabMetadata);
        yield return (openHashTabManifestInfo, openHashTabMetadata);
    }

    private IEnumerable<ManifestInfo> ManifestsMatcher(IEnumerable<ManifestInfo> expected)
    {
        return FluentExtensions.Matcher<IEnumerable<ManifestInfo>>(_ =>
            _.Should().BeEquivalentTo(expected, options => options.Including(_ => _.Id), ""), _testOutputHelper);
    }
}
