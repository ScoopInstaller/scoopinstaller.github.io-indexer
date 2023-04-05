using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Extensions;
using Moq;
using Newtonsoft.Json;
using ScoopSearch.Functions.Data;
using ScoopSearch.Functions.Git;
using ScoopSearch.Functions.Manifest;
using ScoopSearch.Functions.Tests.Helpers;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace ScoopSearch.Functions.Tests.Manifest;

public class ManifestCrawlerTests
{
    private readonly Mock<IGitRepositoryProvider> _gitRepositoryProviderMock;
    private readonly XUnitLogger<ManifestCrawler> _logger;
    private readonly ManifestCrawler _sut;

    public ManifestCrawlerTests(ITestOutputHelper testOutputHelper)
    {
        _gitRepositoryProviderMock = new Mock<IGitRepositoryProvider>();
        var keyGeneratorMock = new Mock<IKeyGenerator>();
        keyGeneratorMock
            .Setup(_ => _.Generate(It.IsAny<ManifestMetadata>()))
            .Returns<ManifestMetadata>(_ => $"KEY_{_.FilePath}");

        _logger = new XUnitLogger<ManifestCrawler>(testOutputHelper);

        _sut = new ManifestCrawler(_gitRepositoryProviderMock.Object, keyGeneratorMock.Object, _logger);
    }

    [Fact]
    public void GetManifestsFromRepository_NullRepository_ReturnsEmptyResults()
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var cancellationToken = new CancellationToken();
        _gitRepositoryProviderMock
            .Setup(_ => _.Download(uri, cancellationToken))
            .Returns((IGitRepository?)null);

        // Act
        var result = _sut.GetManifestsFromRepository(uri, cancellationToken);

        // Assert
        result.Should().BeEmpty();
        _logger.Should().NoLog(LogLevel.Trace);
    }

    [Fact]
    public void GetManifestsFromRepository_ManifestNotInCache_ManifestSkipped()
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var cancellationToken = new CancellationToken();
        var gitRepositoryMock = CreateGitRepositoryMock(new[]
        {
            new GitRepositoryMockEntry("manifest1.json", EntryType.File, null),
            new GitRepositoryMockEntry("manifest2.json", EntryType.File, "{}"),
        }, cancellationToken);
        _gitRepositoryProviderMock
            .Setup(_ => _.Download(uri, cancellationToken))
            .Returns(gitRepositoryMock.Object);

        // Act
        var result = _sut.GetManifestsFromRepository(uri, cancellationToken).ToArray();

        // Assert
        result.Should().HaveCount(1);
        var manifestInfo = result.Single();
        using (new AssertionScope())
        {
            manifestInfo.Id.Should().Be("KEY_manifest2.json");
            manifestInfo.Metadata.AuthorName.Should().Be("authorName_manifest2.json");
            manifestInfo.Metadata.AuthorMail.Should().Be("authorMail_manifest2.json");
            manifestInfo.Metadata.Sha.Should().Be("sha_manifest2.json");
            manifestInfo.Metadata.Committed.Should().BeCloseTo(DateTimeOffset.Now, 1.Seconds());
        }
        _logger.Should().Log(LogLevel.Warning, $"Unable to find a commit for manifest 'manifest1.json' from '{Constants.TestRepositoryUri}'");
    }

    [Fact]
    public void GetManifestsFromRepository_InvalidManifest_ManifestSkipped()
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var cancellationToken = new CancellationToken();
        var gitRepositoryMock = CreateGitRepositoryMock(new[]
        {
            new GitRepositoryMockEntry("manifest1.json", EntryType.File, "invalid"),
            new GitRepositoryMockEntry("manifest2.json", EntryType.File, "{}"),
        }, cancellationToken);
        _gitRepositoryProviderMock
            .Setup(_ => _.Download(uri, cancellationToken))
            .Returns(gitRepositoryMock.Object);

        // Act
        var result = _sut.GetManifestsFromRepository(uri, cancellationToken).ToArray();

        // Assert
        result.Should().HaveCount(1);
        var manifestInfo = result.Single();
        using (new AssertionScope())
        {
            manifestInfo.Id.Should().Be("KEY_manifest2.json");
            manifestInfo.Metadata.AuthorName.Should().Be("authorName_manifest2.json");
            manifestInfo.Metadata.AuthorMail.Should().Be("authorMail_manifest2.json");
            manifestInfo.Metadata.Sha.Should().Be("sha_manifest2.json");
            manifestInfo.Metadata.Committed.Should().BeCloseTo(DateTimeOffset.Now, 1.Seconds());
        }
        _logger.Should().Log<JsonReaderException>(LogLevel.Warning, $"Unable to parse manifest 'manifest1.json' from '{Constants.TestRepositoryUri}'");
    }

    [Fact]
    public void GetManifestsFromRepository_SelectsBucketSubDirectoryIfExists_ReturnsManifestsFromSubDirectoryOnly()
    {
        // Arrange
        var uri = new Uri(Constants.TestRepositoryUri);
        var cancellationToken = new CancellationToken();
        var gitRepositoryMock = CreateGitRepositoryMock(new[]
        {
            new GitRepositoryMockEntry("bucket/manifest1.json", EntryType.File, "{}"),
            new GitRepositoryMockEntry("manifest2.json", EntryType.File, "{}"),
            new GitRepositoryMockEntry("bucket", EntryType.Directory, null),
        }, cancellationToken);
        _gitRepositoryProviderMock
            .Setup(_ => _.Download(uri, cancellationToken))
            .Returns(gitRepositoryMock.Object);

        // Act
        var result = _sut.GetManifestsFromRepository(uri, cancellationToken).ToArray();

        // Assert
        result.Should().HaveCount(1);
        var manifestInfo = result.Single();
        using (new AssertionScope())
        {
            manifestInfo.Id.Should().Be("KEY_bucket/manifest1.json");
            manifestInfo.Metadata.AuthorName.Should().Be("authorName_bucket/manifest1.json");
            manifestInfo.Metadata.AuthorMail.Should().Be("authorMail_bucket/manifest1.json");
            manifestInfo.Metadata.Sha.Should().Be("sha_bucket/manifest1.json");
            manifestInfo.Metadata.Committed.Should().BeCloseTo(DateTimeOffset.Now, 1.Seconds());
        }
    }

    private record GitRepositoryMockEntry(string Path, EntryType Type, string? Content);

    private Mock<IGitRepository> CreateGitRepositoryMock(GitRepositoryMockEntry[] entries, CancellationToken cancellationToken)
    {
        var gitRepositoryMock = new Mock<IGitRepository>();
        gitRepositoryMock
            .Setup(_ => _.GetEntriesFromIndex())
            .Returns(entries.Select(_ => new Entry(_.Path, _.Type)));
        gitRepositoryMock
            .Setup(_ => _.GetCommitsCache(It.IsAny<Predicate<string>>(), cancellationToken))
            .Returns(entries.Where(_ => _.Content != null).ToDictionary(
                k => k.Path,
                v => (IReadOnlyCollection<CommitInfo>)new[]
                {
                    new CommitInfo($"authorName_{v.Path}", $"authorMail_{v.Path}", DateTimeOffset.Now, $"sha_{v.Path}")
                }));
        foreach (var entry in entries.Where(_ => _.Content != null))
        {
            gitRepositoryMock
                .Setup(_ => _.ReadContent(It.Is<Entry>(_ => _.Path == entry.Path)))
                .Returns(entry.Content!);
        }

        return gitRepositoryMock;
    }


}
