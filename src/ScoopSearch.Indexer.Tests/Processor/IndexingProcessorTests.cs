using Bogus;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.Indexer;
using ScoopSearch.Indexer.Processor;
using ScoopSearch.Indexer.Tests.Helpers;
using Xunit.Abstractions;
using Faker = ScoopSearch.Indexer.Tests.Helpers.Faker;

namespace ScoopSearch.Indexer.Tests.Processor;

public class IndexingProcessorTests : IClassFixture<HostFixture>
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Mock<ISearchClient> _searchClientMock;
    private readonly Mock<ISearchIndex> _searchIndexMock;
    private readonly XUnitLogger<IndexingProcessor> _logger;
    private readonly IndexingProcessor _sut;

    public IndexingProcessorTests(HostFixture hostFixture, ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        hostFixture.Configure(testOutputHelper);

        _searchClientMock = new Mock<ISearchClient>();
        _searchIndexMock = new Mock<ISearchIndex>();
        _logger = new XUnitLogger<IndexingProcessor>(testOutputHelper);
        _sut = new IndexingProcessor(
            _searchClientMock.Object,
            _searchIndexMock.Object,
            _logger);
    }

    [Fact]
    public async Task CreateIndexIfRequiredAsync_Succeeds()
    {
        // Arrange
        var cancellationToken = new CancellationToken();

        // Act
        await _sut.CreateIndexIfRequiredAsync(cancellationToken);

        // Assert
        _searchIndexMock.Verify(_ => _.CreateIndexIfRequiredAsync(cancellationToken), Times.Once);
    }

    [Theory]
    [MemberData(nameof(GetRunTestCases))]
    public async Task UpdateIndexWithManifestsAsync_IndexUpdated(
        ManifestInfo[] manifestsInIndex,
        int expectedManifestsToAddCount,
        int expectedManifestsToUpdateCount,
        ManifestInfo[] expectedManifestsToAddOrUpdate,
        ManifestInfo[] expectedManifestsToRemove)
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        _searchClientMock
            .Setup(_ => _.GetAllManifestsAsync(cancellationToken))
            .Returns(manifestsInIndex.ToAsyncEnumerable())
            .Verifiable();
        var manifestsInRepositories = CreateFakeData().Select(_ => _.ManifestInfo.Generate()).ToArray();

        // Act
        await _sut.UpdateIndexWithManifestsAsync(manifestsInRepositories, cancellationToken);

        // Assert
        _logger.Should().Log(LogLevel.Information, $"{manifestsInIndex.Length} manifests found in the index");
        _logger.Should().Log(LogLevel.Information, $"{expectedManifestsToRemove.Length} manifests to delete from the index (not found in the existing buckets anymore)");
        _logger.Should().Log(LogLevel.Information, $"{expectedManifestsToAddCount} manifests to add to the index");
        _logger.Should().Log(LogLevel.Information, $"{expectedManifestsToUpdateCount} manifests to update in the index");

        if (expectedManifestsToAddOrUpdate.Length > 0)
        {
            _searchClientMock.Verify(_ => _.UpsertManifestsAsync(ManifestsMatcher(expectedManifestsToAddOrUpdate), cancellationToken));
        }

        if (expectedManifestsToRemove.Length > 0)
        {
            _searchClientMock.Verify(_ => _.DeleteManifestsAsync(ManifestsMatcher(expectedManifestsToRemove), cancellationToken));
        }

        _searchClientMock.Verify();
        _searchClientMock.VerifyNoOtherCalls();
    }

    public static TheoryData<ManifestInfo[], int, int, ManifestInfo[], ManifestInfo[]> GetRunTestCases()
    {
        var data = new TheoryData<ManifestInfo[], int, int, ManifestInfo[], ManifestInfo[]>();

        ManifestInfo[] manifestsInIndex;
        ManifestInfo[] expectedManifestsToAddOrUpdate;
        ManifestInfo[] expectedManifestsToRemove;
        (Faker<ManifestInfo> ManifestInfo, Faker<ManifestMetadata> ManifestMetadata)[] fakeData;

        // Add all manifests to the index
        fakeData = CreateFakeData().ToArray();
        manifestsInIndex = Array.Empty<ManifestInfo>();
        expectedManifestsToAddOrUpdate = fakeData.Select(_ => _.ManifestInfo.Generate()).ToArray();
        expectedManifestsToRemove = Array.Empty<ManifestInfo>();
        data.Add(manifestsInIndex, 5, 0, expectedManifestsToAddOrUpdate, expectedManifestsToRemove);

        // All manifests already in the index and up to date
        fakeData = CreateFakeData().ToArray();
        manifestsInIndex = fakeData.Select(_ => _.ManifestInfo.Generate()).ToArray();
        expectedManifestsToAddOrUpdate = Array.Empty<ManifestInfo>();
        expectedManifestsToRemove = Array.Empty<ManifestInfo>();
        data.Add(manifestsInIndex, 0, 0, expectedManifestsToAddOrUpdate, expectedManifestsToRemove);

        // All manifests already in the index and not all are up to date
        fakeData = CreateFakeData().ToArray();
        fakeData[1].ManifestMetadata.RuleFor(_ => _.RepositoryStars, 0);
        fakeData[2].ManifestMetadata.RuleFor(_ => _.Sha, f => f.Random.Hash());
        fakeData[3].ManifestMetadata.RuleFor(_ => _.OfficialRepositoryNumber, 1);
        manifestsInIndex = fakeData.Select(_ => _.ManifestInfo.Generate()).ToArray();
        expectedManifestsToAddOrUpdate = new[] { manifestsInIndex[1], manifestsInIndex[2], manifestsInIndex[3] };
        expectedManifestsToRemove = Array.Empty<ManifestInfo>();
        data.Add(manifestsInIndex, 0, 3, expectedManifestsToAddOrUpdate, expectedManifestsToRemove);

        // Some manifests in the index but not on the repo anymore
        fakeData = CreateFakeData().ToArray();
        manifestsInIndex = fakeData
            .Select(_ => _.ManifestInfo.Generate())
            .Concat(new[] { Faker.CreateManifestInfo().Generate() })
            .ToArray();
        expectedManifestsToAddOrUpdate = Array.Empty<ManifestInfo>();
        expectedManifestsToRemove = new[] { manifestsInIndex[5] };
        data.Add(manifestsInIndex, 0, 0, expectedManifestsToAddOrUpdate, expectedManifestsToRemove);

        // Mix everything
        fakeData = CreateFakeData().ToArray();
        fakeData[1].ManifestMetadata.RuleFor(_ => _.RepositoryStars, 0);
        fakeData[2].ManifestMetadata.RuleFor(_ => _.Sha, f => f.Random.Hash());
        fakeData[3].ManifestMetadata.RuleFor(_ => _.OfficialRepositoryNumber, 1);
        var otherManifestInfo = Faker.CreateManifestInfo().Generate();
        manifestsInIndex = new[]
        {
            fakeData[0].ManifestInfo.Generate(), // Original
            fakeData[1].ManifestInfo.Generate(), // stars changed
            fakeData[2].ManifestInfo.Generate(), // sha changed
            fakeData[3].ManifestInfo.Generate(), // official changed
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
        data.Add(manifestsInIndex, 1, 3, expectedManifestsToAddOrUpdate, expectedManifestsToRemove);

        return data;
    }

    private static IEnumerable<(Faker<ManifestInfo> ManifestInfo, Faker<ManifestMetadata> ManifestMetadata)> CreateFakeData()
    {
        var cdexMetadata = Faker.CreateManifestMetadata()
            .RuleFor(_ => _.Sha, "0c91acbccc09ddac3bcce55970ff9c4f811012ec")
            .RuleFor(_ => _.RepositoryStars, 123)
            .RuleFor(_ => _.OfficialRepositoryNumber, 0);
        var cdexManifestInfo = Faker.CreateManifestInfo()
            .RuleFor(_ => _.Id, "19acee9283bf6185ff60a0960cc4b196b185b688")
            .RuleFor(_ => _.Name, "cdex")
            .RuleFor(_ => _.Metadata, () => cdexMetadata);
        yield return (cdexManifestInfo, cdexMetadata);

        var cfgCamMetadata = Faker.CreateManifestMetadata()
            .RuleFor(_ => _.Sha, "f6d6b8ac0ddd2dd66a4610421c9d526a55bcbe6c")
            .RuleFor(_ => _.RepositoryStars, 123)
            .RuleFor(_ => _.OfficialRepositoryNumber, 0);
        var cfgCamManifestInfo = Faker.CreateManifestInfo()
            .RuleFor(_ => _.Id, "6aeea6969109db70a61a61ca1d851acddf03aa34")
            .RuleFor(_ => _.Name, "cdg-cam")
            .RuleFor(_ => _.Metadata, () => cfgCamMetadata);
        yield return (cfgCamManifestInfo, cfgCamMetadata);

        var dnsProxyMetadata = Faker.CreateManifestMetadata()
            .RuleFor(_ => _.Sha, "83d1afa833e572c55a3954dfe8cd1d2b4e1e749b")
            .RuleFor(_ => _.RepositoryStars, 123)
            .RuleFor(_ => _.OfficialRepositoryNumber, 0);
        var dnsProxyManifestInfo = Faker.CreateManifestInfo()
            .RuleFor(_ => _.Id, "2f0fe5924b8f879f9944fb78d79c63f6a970a81d")
            .RuleFor(_ => _.Name, "dnsproxy")
            .RuleFor(_ => _.Metadata, () => dnsProxyMetadata);
        yield return (dnsProxyManifestInfo, dnsProxyMetadata);

        var kaxamlMetadata = Faker.CreateManifestMetadata()
            .RuleFor(_ => _.Sha, "c9741fe372820c045ade167f6fc7727e384e6238")
            .RuleFor(_ => _.RepositoryStars, 123)
            .RuleFor(_ => _.OfficialRepositoryNumber, 0);
        var kaxamlManifestInfo = Faker.CreateManifestInfo()
            .RuleFor(_ => _.Id, "b6b519ecf87a20fffdd1d799aaf78eec4817a8d1")
            .RuleFor(_ => _.Name, "kaxaml")
            .RuleFor(_ => _.Metadata, () => kaxamlMetadata);
        yield return (kaxamlManifestInfo, kaxamlMetadata);

        var openHashTabMetadata = Faker.CreateManifestMetadata()
            .RuleFor(_ => _.Sha, "35f8371051986f6a89017bb901e44e1deaf06111")
            .RuleFor(_ => _.RepositoryStars, 123)
            .RuleFor(_ => _.OfficialRepositoryNumber, 0);
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
