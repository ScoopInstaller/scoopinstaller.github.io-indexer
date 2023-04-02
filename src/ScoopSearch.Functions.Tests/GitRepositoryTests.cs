using System.Diagnostics;
using System.Reflection;
using FluentAssertions;
using FluentAssertions.Extensions;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using ScoopSearch.Functions.Data;
using ScoopSearch.Functions.Git;
using ScoopSearch.Functions.Tests.Helpers;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace ScoopSearch.Functions.Tests;

public class GitRepositoryTests : IDisposable
{
    private readonly XUnitLogger<GitRepository> _logger;
    private readonly string _repositoriesRoot;
    private readonly GitRepository _sut;

    public GitRepositoryTests(ITestOutputHelper testOutputHelper)
    {
        _logger = new XUnitLogger<GitRepository>(testOutputHelper);

        _repositoriesRoot = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "repositoriesTests", Guid.NewGuid().ToString());
        _logger.LogInformation($"Repositories root: {_repositoriesRoot}");

        _sut = new GitRepository(_logger, _repositoriesRoot);
    }

    public void Dispose()
    {
        _logger.LogInformation($"Deleting repositories root: {_repositoriesRoot}");
        if (Directory.Exists(_repositoriesRoot))
        {
            Directory.Delete(_repositoriesRoot, true);
        }
    }

    [Fact]
    public void DownloadRepository_NonExistentRepository_ReturnsNull()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.NonExistentTestRepositoryUri);

        // Act
        var repositoryDirectory = _sut.DownloadRepository(repositoryUri, CancellationToken.None);

        // Assert
        repositoryDirectory.Should().BeNull();
        _logger.Should()
            .Log<LibGit2SharpException>(LogLevel.Error, _ => _.StartsWith($"Unable to clone repository '{repositoryUri}' to"));
    }

    [Fact]
    public void DownloadRepository_ValidRepository_ReturnsRepositoryDirectory()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.TestRepositoryUri);

        // Act
        var repositoryDirectory = _sut.DownloadRepository(repositoryUri, CancellationToken.None);

        // Assert
        repositoryDirectory.Should().NotBeNull();
        Directory.Exists(repositoryDirectory).Should().BeTrue();
        _logger.Should()
            .Log(LogLevel.Debug, $"Cloning repository '{repositoryUri}' in '{repositoryDirectory}'")
            .And.NoLog(LogLevel.Warning);
    }

    [Fact]
    public void DownloadRepository_ValidExistingDirectoryRepository_ReturnsRepositoryDirectory()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.TestRepositoryUri);
        var expectedRepositoryDirectory = Path.Combine(_repositoriesRoot, repositoryUri.AbsolutePath[1..]);
        Repository.Clone(Constants.TestRepositoryUri, expectedRepositoryDirectory);

        // Act
        var actualRepositoryDirectory = _sut.DownloadRepository(repositoryUri, CancellationToken.None);

        // Assert
        actualRepositoryDirectory.Should().Be(expectedRepositoryDirectory);
        _logger.Should()
            .Log(LogLevel.Debug, $"Pulling repository '{expectedRepositoryDirectory}'")
            .And.NoLog(LogLevel.Warning);;
    }

    [Fact]
    public void DownloadRepository_Cancellation_ReturnsNull()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.TestRepositoryUri);
        var expectedRepositoryDirectory = Path.Combine(_repositoriesRoot, repositoryUri.AbsolutePath[1..]);
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        // Act
        var actualRepositoryDirectory = _sut.DownloadRepository(repositoryUri, cts.Token);

        // Assert
        actualRepositoryDirectory.Should().BeNull();
        _logger.Should().Log<UserCancelledException>(
            LogLevel.Error,
            $"Unable to clone repository '{repositoryUri}' to '{expectedRepositoryDirectory}'");
    }

    [Fact]
    public void DownloadRepository_CorruptedExistingDirectoryRepository_ReturnsDirectoryRepository()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.TestRepositoryUri);
        var expectedRepositoryDirectory = Path.Combine(_repositoriesRoot, repositoryUri.AbsolutePath[1..]);
        Directory.CreateDirectory(expectedRepositoryDirectory);

        // Act
        var actualRepositoryDirectory = _sut.DownloadRepository(repositoryUri, CancellationToken.None);

        // Assert
        actualRepositoryDirectory.Should().NotBeNull();
        _logger.Should()
            .Log<RepositoryNotFoundException>(
                LogLevel.Warning,
                $"Unable to pull repository '{Constants.TestRepositoryUri}' to '{actualRepositoryDirectory}'")
            .And.Log(LogLevel.Debug, $"Cloning repository '{Constants.TestRepositoryUri}' in '{actualRepositoryDirectory}'");
    }

    [Fact]
    public void DownloadRepository_EmptyRepository_ReturnsNull()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.EmptyTestRepositoryUri);

        // Act
        var actualRepositoryDirectory = _sut.DownloadRepository(repositoryUri, CancellationToken.None);

        // Assert
        actualRepositoryDirectory.Should().BeNull();
        _logger.Should()
            .Log(LogLevel.Error, _ => _.StartsWith("No remote branch found for repository "));
    }

    [Fact]
    public void DeleteRepository_ExistingRepository_Deleted()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.TestRepositoryUri);
        var repositoryDirectory = _sut.DownloadRepository(repositoryUri, CancellationToken.None);

        // Assert
        Directory.Exists(repositoryDirectory).Should().BeTrue();

        // Act
        _sut.DeleteRepository(repositoryDirectory!);

        // Assert
        Directory.Exists(repositoryDirectory).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(GetCommitsCacheTestCases))]
    public void GetCommitsCache_ReturnsExpectedFilesAndCommits(string repositoryUri, Predicate<string> filter, int expectedFiles, int expectedCommits)
    {
        // Arrange
        var repositoryDirectory = _sut.DownloadRepository(new Uri(repositoryUri), CancellationToken.None);
        using var repository = new Repository(repositoryDirectory);

        // Act
        var cache = _sut.GetCommitsCache(repository, filter, CancellationToken.None);

        // Assert
        cache.Should().HaveCount(expectedFiles);
        cache.SelectMany(_ => _.Value).DistinctBy(_ => _.Sha).Should().HaveCount(expectedCommits);
    }

    public static IEnumerable<object[]> GetCommitsCacheTestCases()
    {
        // repository, filter, expected files, expected commits
        yield return new object[] { Constants.TestRepositoryUri, new Predicate<string>(_ => true), 14, 39 };
        yield return new object[] { Constants.TestRepositoryUri, new Predicate<string>(_ => false), 0, 0 };
        yield return new object[] { Constants.TestRepositoryUri, new Predicate<string>(_ => _.EndsWith(".json")), 11, 30 };
    }

    [Theory]
    [InlineData(Constants.TestRepositoryUri, 1, 5)]
    [InlineData("https://github.com/niheaven/scoop-sysinternals", 1, 70)]
    [InlineData("https://github.com/ScoopInstaller/Extras", 5, 1_900)]
    public void GetCommitsCache_BuildCache_Succeeds(string repositoryUri, double maxSeconds, int minimalManifestsCount)
    {
        // Arrange
        var repositoryDirectory = _sut.DownloadRepository(new Uri(repositoryUri), CancellationToken.None);
        using var repository = new Repository(repositoryDirectory);
        bool IsManifestFile(string filePath) => Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase);

        // Act
        IReadOnlyDictionary<string, IReadOnlyCollection<CommitInfo>>? cache = null;
        Action action = () => cache = _sut.GetCommitsCache(repository, IsManifestFile, CancellationToken.None);

        // Assert
        action.ExecutionTime().Should().BeLessThan(maxSeconds.Seconds());
        cache.Should().HaveCountGreaterThan(minimalManifestsCount);
    }
}
