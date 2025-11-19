using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Extensions;
using LibGit2Sharp;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoopSearch.Indexer.Buckets;
using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.Git;
using ScoopSearch.Indexer.Manifest;
using ScoopSearch.Indexer.Processor;
using ScoopSearch.Indexer.Tests.Helpers;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace ScoopSearch.Indexer.Tests.Processor;

public class FetchManifestsProcessorTests : IClassFixture<HostFixture>
{
    private readonly HostFixture _hostFixture;
    private readonly XUnitLogger<FetchManifestsProcessor> _logger;
    private readonly XUnitLogger<GitRepository> _gitRepositoryLogger;

    public FetchManifestsProcessorTests(HostFixture hostFixture)
    {
        _hostFixture = hostFixture;

        _logger = new XUnitLogger<FetchManifestsProcessor>();
        _gitRepositoryLogger = new XUnitLogger<GitRepository>();
    }

    [Fact]
    public async Task FetchManifestsAsync_ValidRepository_ReturnsManifests()
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var bucket = new Bucket(uri, 0);
        var cancellationToken = new CancellationToken();
        var sut = CreateSut();

        // Act
        var result = await sut.FetchManifestsAsync(bucket, cancellationToken).ToArrayAsync(cancellationToken);

        // Assert
        _logger.Should().Log(LogLevel.Debug, $"Generating manifests list for {uri}");
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task FetchManifestsAsync_EmptyRepository_ReturnsEmptyResults()
    {
        // Arrange
        var uri = new Uri(Constants.EmptyTestRepositoryUri);
        var bucket = new Bucket(uri, 0);
        var cancellationToken = new CancellationToken();
        var sut = CreateSut();

        // Act
        var result = await sut.FetchManifestsAsync(bucket, cancellationToken).ToArrayAsync(cancellationToken);

        // Assert
        _gitRepositoryLogger.Should().Log(LogLevel.Error, message => message.StartsWith("No valid branch found in"));
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchManifestsAsync_NonExistentRepository_ReturnsEmptyResults()
    {
        // Arrange
        var uri = new Uri(Constants.NonExistentTestRepositoryUri);
        var bucket = new Bucket(uri, 0);
        var cancellationToken = new CancellationToken();
        var sut = CreateSut();

        // Act
        var result = await sut.FetchManifestsAsync(bucket, cancellationToken).ToArrayAsync(cancellationToken);

        // Assert
        _gitRepositoryLogger.Should().Log<LibGit2SharpException>(LogLevel.Error, message => message.StartsWith("Unable to clone repository"));
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchManifestsAsync_NullRepository_ReturnsEmptyResults()
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var bucket = new Bucket(uri, 0);
        var cancellationToken = new CancellationToken();
        var sut = CreateSut(mockConfig => mockConfig
            .Setup(_ => _.Download(uri, cancellationToken))
            .Returns((IGitRepository?)null));

        // Act
        var result = await sut.FetchManifestsAsync(bucket, cancellationToken).ToArrayAsync(cancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchManifestsAsync_ManifestNotInCommitsCache_ManifestSkipped()
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var bucket = new Bucket(uri, 0);
        var cancellationToken = new CancellationToken();
        var gitRepositoryMock = CreateGitRepositoryMock(new[]
        {
            new GitRepositoryMockEntry("manifest1.json", null),
            new GitRepositoryMockEntry("manifest2.json", "{}"),
        }, cancellationToken);

        var sut = CreateSut(mockConfig => mockConfig
            .Setup(_ => _.Download(uri, cancellationToken))
            .Returns(gitRepositoryMock.Object));

        // Act
        var result = await sut.FetchManifestsAsync(bucket, cancellationToken).ToArrayAsync(cancellationToken);

        // Assert
        result.Should().HaveCount(1);
        var manifestInfo = result.Single();
        using (new AssertionScope())
        {
            manifestInfo.Id.Should().Be("KEY_manifest2.json");
            manifestInfo.Metadata.Sha.Should().Be("sha_manifest2.json");
            manifestInfo.Metadata.Committed.Should().BeCloseTo(DateTimeOffset.Now, 1.Seconds());
        }
        _logger.Should().Log(LogLevel.Warning, $"Unable to find a commit for manifest manifest1.json from {Constants.TestRepositoryUri}");
    }

    [Fact]
    public async Task FetchManifestsAsync_InvalidManifest_ManifestSkipped()
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var bucket = new Bucket(uri, 0);
        var cancellationToken = new CancellationToken();
        var gitRepositoryMock = CreateGitRepositoryMock(new[]
        {
            new GitRepositoryMockEntry("manifest1.json", "invalid"),
            new GitRepositoryMockEntry("manifest2.json", "{}"),
        }, cancellationToken);

        var sut = CreateSut(mockConfig => mockConfig
            .Setup(_ => _.Download(uri, cancellationToken))
            .Returns(gitRepositoryMock.Object));

        // Act
        var result = await sut.FetchManifestsAsync(bucket, cancellationToken).ToArrayAsync(cancellationToken);

        // Assert
        result.Should().HaveCount(1);
        var manifestInfo = result.Single();
        using (new AssertionScope())
        {
            manifestInfo.Id.Should().Be("KEY_manifest2.json");
            manifestInfo.Metadata.Sha.Should().Be("sha_manifest2.json");
            manifestInfo.Metadata.Committed.Should().BeCloseTo(DateTimeOffset.Now, 1.Seconds());
        }
        _logger.Should().Log<JsonException>(LogLevel.Error, $"Unable to parse manifest manifest1.json from {Constants.TestRepositoryUri}");
    }

    [Fact]
    public async Task FetchManifestsAsync_SelectsBucketSubDirectoryIfExists_ReturnsManifestsFromSubDirectoryOnly()
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var bucket = new Bucket(uri, 0);
        var cancellationToken = new CancellationToken();
        var gitRepositoryMock = CreateGitRepositoryMock(new[]
        {
            new GitRepositoryMockEntry("bucket/manifest1.json", "{}"),
            new GitRepositoryMockEntry("manifest2.json", "{}"),
        }, cancellationToken);
        var sut = CreateSut(mockConfig => mockConfig
            .Setup(_ => _.Download(uri, cancellationToken))
            .Returns(gitRepositoryMock.Object));

        // Act
        var result = await sut.FetchManifestsAsync(bucket, cancellationToken).ToArrayAsync(cancellationToken);

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
            .Returns(entries.Select(entry => entry.Path));
        gitRepositoryMock
            .Setup(_ => _.GetCommitsCacheAsync(It.IsAny<Predicate<string>>(), cancellationToken))
            .ReturnsAsync(entries.Where(entry => entry.Content != null).ToDictionary(
                kv => kv.Path,
                kv => (IReadOnlyCollection<CommitInfo>)new[] { new CommitInfo(DateTimeOffset.Now, $"sha_{kv.Path}") }));
        foreach (var entry in entries.Where(entry => entry.Content != null))
        {
            gitRepositoryMock
                .Setup(_ => _.ReadContentAsync(It.Is<string>(filePath => filePath == entry.Path), cancellationToken))
                .ReturnsAsync(entry.Content!);
        }

        return gitRepositoryMock;
    }

    private FetchManifestsProcessor CreateSut()
    {
        return new FetchManifestsProcessor(
            new GitRepositoryProvider(_gitRepositoryLogger),
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
            .Returns<ManifestMetadata>(manifestMetadata => $"KEY_{manifestMetadata.FilePath}");

        return new FetchManifestsProcessor(
            gitRepositoryProviderMock.Object,
            keyGeneratorMock.Object,
            _logger);
    }
}
