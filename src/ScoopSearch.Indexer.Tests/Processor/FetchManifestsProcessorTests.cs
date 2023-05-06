using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.Git;
using ScoopSearch.Indexer.Manifest;
using ScoopSearch.Indexer.Processor;
using ScoopSearch.Indexer.Tests.Helpers;
using Xunit.Abstractions;

namespace ScoopSearch.Indexer.Tests.Processor;

public class FetchManifestsProcessorTests : IClassFixture<HostFixture>
{
    private readonly HostFixture _hostFixture;
    private readonly XUnitLogger<FetchManifestsProcessor> _logger;

    public FetchManifestsProcessorTests(HostFixture hostFixture, ITestOutputHelper testOutputHelper)
    {
        _hostFixture = hostFixture;
        _hostFixture.Configure(testOutputHelper);

        _logger = new XUnitLogger<FetchManifestsProcessor>(testOutputHelper);
    }

    [Theory]
    [CombinatorialData]
    public async void FetchManifestsAsync_ValidRepository_ReturnsManifestsWithStarsAndKind([CombinatorialValues(123, 321)] int stars, bool officialRepository)
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var bucketInfo = new BucketInfo(uri, stars, officialRepository);
        var cancellationToken = CancellationToken.None;
        var sut = CreateSut();

        // Act
        var result = await sut.FetchManifestsAsync(bucketInfo, cancellationToken);

        // Assert
        _logger.Should().Log(LogLevel.Information, $"Found 5 manifests for {uri}");
        result
            .Should().HaveCount(5)
            .And.AllSatisfy(_ =>
            {
                _.Metadata.RepositoryStars.Should().Be(stars);
                _.Metadata.OfficialRepository.Should().Be(officialRepository);
            });
    }

    [Theory]
    [InlineData(Constants.NonExistentTestRepositoryUri)]
    [InlineData(Constants.EmptyTestRepositoryUri)]
    public async void FetchManifestsAsync_InvalidRepository_ReturnsEmptyResults(string repository)
    {
        // Arrange
        var uri = new Uri(repository);
        var bucketInfo = new BucketInfo(uri, 0, false);
        var cancellationToken = CancellationToken.None;
        var sut = CreateSut();

        // Act
        var result = await sut.FetchManifestsAsync(bucketInfo, cancellationToken);

        // Assert
        _logger.Should().Log(LogLevel.Information, $"Found 0 manifests for {uri}");
        result.Should().BeEmpty();
    }

    [Fact]
    public async void FetchManifestsAsync_NullRepository_ReturnsEmptyResults()
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var bucketInfo = new BucketInfo(uri, 0, false);
        var cancellationToken = new CancellationToken();
        var sut = CreateSut(_ => _
            .Setup(_ => _.Download(uri, cancellationToken))
            .Returns((IGitRepository?)null));

        // Act
        var result = await sut.FetchManifestsAsync(bucketInfo, cancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async void FetchManifestsAsync_ManifestNotInCommitsCache_ManifestSkipped()
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var bucketInfo = new BucketInfo(uri, 0, false);
        var cancellationToken = new CancellationToken();
        var gitRepositoryMock = CreateGitRepositoryMock(new[]
        {
            new GitRepositoryMockEntry("manifest1.json", null),
            new GitRepositoryMockEntry("manifest2.json", "{}"),
        }, cancellationToken);

        var sut = CreateSut(_ => _
            .Setup(_ => _.Download(uri, cancellationToken))
            .Returns(gitRepositoryMock.Object));

        // Act
        var result = await sut.FetchManifestsAsync(bucketInfo, cancellationToken);

        // Assert
        result.Should().HaveCount(1);
        var manifestInfo = result.Single();
        using (new AssertionScope())
        {
            manifestInfo.Id.Should().Be("KEY_manifest2.json");
            manifestInfo.Metadata.Sha.Should().Be("sha_manifest2.json");
            manifestInfo.Metadata.Committed.Should().BeCloseTo(DateTimeOffset.Now, 1.Seconds());
        }
        _logger.Should().Log(LogLevel.Warning, $"Unable to find a commit for manifest 'manifest1.json' from '{Constants.TestRepositoryUri}'");
    }

    [Fact]
    public async void FetchManifestsAsync_InvalidManifest_ManifestSkipped()
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var bucketInfo = new BucketInfo(uri, 0, false);
        var cancellationToken = new CancellationToken();
        var gitRepositoryMock = CreateGitRepositoryMock(new[]
        {
            new GitRepositoryMockEntry("manifest1.json", "invalid"),
            new GitRepositoryMockEntry("manifest2.json", "{}"),
        }, cancellationToken);

        var sut = CreateSut(_ => _
            .Setup(_ => _.Download(uri, cancellationToken))
            .Returns(gitRepositoryMock.Object));

        // Act
        var result = await sut.FetchManifestsAsync(bucketInfo, cancellationToken);

        // Assert
        result.Should().HaveCount(1);
        var manifestInfo = result.Single();
        using (new AssertionScope())
        {
            manifestInfo.Id.Should().Be("KEY_manifest2.json");
            manifestInfo.Metadata.Sha.Should().Be("sha_manifest2.json");
            manifestInfo.Metadata.Committed.Should().BeCloseTo(DateTimeOffset.Now, 1.Seconds());
        }
        _logger.Should().Log<JsonException>(LogLevel.Error, $"Unable to parse manifest 'manifest1.json' from '{Constants.TestRepositoryUri}'");
    }

    [Fact]
    public async void FetchManifestsAsync_SelectsBucketSubDirectoryIfExists_ReturnsManifestsFromSubDirectoryOnly()
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var bucketInfo = new BucketInfo(uri, 0, false);
        var cancellationToken = new CancellationToken();
        var gitRepositoryMock = CreateGitRepositoryMock(new[]
        {
            new GitRepositoryMockEntry("bucket/manifest1.json", "{}"),
            new GitRepositoryMockEntry("manifest2.json", "{}"),
        }, cancellationToken);
        var sut = CreateSut(_ => _
            .Setup(_ => _.Download(uri, cancellationToken))
            .Returns(gitRepositoryMock.Object));

        // Act
        var result = await sut.FetchManifestsAsync(bucketInfo, cancellationToken);

        // Assert
        result.Should().HaveCount(1);
        var manifestInfo = result.Single();
        using (new AssertionScope())
        {
            manifestInfo.Id.Should().Be("KEY_bucket/manifest1.json");
            manifestInfo.Metadata.Sha.Should().Be("sha_bucket/manifest1.json");
            manifestInfo.Metadata.Committed.Should().BeCloseTo(DateTimeOffset.Now, 1.Seconds());
        }
    }

    private record GitRepositoryMockEntry(string Path, string? Content);

    private Mock<IGitRepository> CreateGitRepositoryMock(GitRepositoryMockEntry[] entries, CancellationToken cancellationToken)
    {
        var gitRepositoryMock = new Mock<IGitRepository>();
        gitRepositoryMock
            .Setup(_ => _.GetFilesFromIndex())
            .Returns(entries.Select(_ => _.Path));
        gitRepositoryMock
            .Setup(_ => _.GetCommitsCache(It.IsAny<Predicate<string>>(), cancellationToken))
            .Returns(entries.Where(_ => _.Content != null).ToDictionary(
                k => k.Path,
                v => (IReadOnlyCollection<CommitInfo>)new[]
                {
                    new CommitInfo(DateTimeOffset.Now, $"sha_{v.Path}")
                }));
        foreach (var entry in entries.Where(_ => _.Content != null))
        {
            gitRepositoryMock
                .Setup(_ => _.ReadContent(It.Is<string>(_ => _ == entry.Path)))
                .Returns(entry.Content!);
        }

        return gitRepositoryMock;
    }

    private FetchManifestsProcessor CreateSut()
    {
        return new FetchManifestsProcessor(
            _hostFixture.Instance.Services.GetRequiredService<IGitRepositoryProvider>(),
            _hostFixture.Instance.Services.GetRequiredService<IKeyGenerator>(),
            _logger);
    }

    private FetchManifestsProcessor CreateSut(Action<Mock<IGitRepositoryProvider>> configureGitRepositoryProvider)
    {
        var gitRepositoryProviderMock = new Mock<IGitRepositoryProvider>();
        configureGitRepositoryProvider(gitRepositoryProviderMock);

        var keyGeneratorMock = new Mock<IKeyGenerator>();
        keyGeneratorMock
            .Setup(_ => _.Generate(It.IsAny<ManifestMetadata>()))
            .Returns<ManifestMetadata>(_ => $"KEY_{_.FilePath}");

        return new FetchManifestsProcessor(
            gitRepositoryProviderMock.Object,
            keyGeneratorMock.Object,
            _logger);
    }
}
