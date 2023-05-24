using System.Reflection;
using FluentAssertions;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using ScoopSearch.Indexer.Git;
using ScoopSearch.Indexer.Tests.Helpers;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace ScoopSearch.Indexer.Tests.Git;

public class GitRepositoryFactoryTests : IDisposable
{
    private readonly XUnitLogger<GitRepository> _logger;
    private readonly string _repositoriesDirectory;
    private readonly GitRepositoryProvider _sut;

    public GitRepositoryFactoryTests(ITestOutputHelper testOutputHelper)
    {
        _logger = new XUnitLogger<GitRepository>(testOutputHelper);

        _repositoriesDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "repositoriesTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_repositoriesDirectory);
        _logger.LogDebug("Repositories root: {RepositoriesDirectory}", _repositoriesDirectory);

        _sut = new GitRepositoryProvider(_logger, _repositoriesDirectory);
    }

    public void Dispose()
    {
        _logger.LogDebug("Deleting repositories root: {RepositoriesDirectory}", _repositoriesDirectory);
        Directory.Delete(_repositoriesDirectory, true);
    }

    [Fact]
    public void Download_NonExistentRepository_ReturnsNull()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.NonExistentTestRepositoryUri);

        // Act
        var result = _sut.Download(repositoryUri, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _logger.Should()
            .Log<LibGit2SharpException>(LogLevel.Error, message => message.StartsWith($"Unable to clone repository {repositoryUri} to"));
    }

    [Fact]
    public void Download_ValidRepository_ReturnsRepositoryDirectory()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.TestRepositoryUri);
        var expectedRepositoryDirectory = Path.Combine(_repositoriesDirectory, repositoryUri.AbsolutePath[1..]);

        // Act
        var result = _sut.Download(repositoryUri, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        Directory.Exists(expectedRepositoryDirectory).Should().BeTrue();
        _logger.Should()
            .Log(LogLevel.Debug, $"Cloning repository {repositoryUri} in {expectedRepositoryDirectory}")
            .And.NoLog(LogLevel.Warning);
    }

    [Fact]
    public void Download_ValidExistingDirectoryRepository_ReturnsRepositoryDirectory()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.TestRepositoryUri);
        var expectedRepositoryDirectory = Path.Combine(_repositoriesDirectory, repositoryUri.AbsolutePath[1..]);
        Repository.Clone(Constants.TestRepositoryUri, expectedRepositoryDirectory);

        // Act
        var result = _sut.Download(repositoryUri, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _logger.Should()
            .Log(LogLevel.Debug, $"Pulling repository {expectedRepositoryDirectory}")
            .And.NoLog(LogLevel.Warning);
    }

    [Fact]
    public void Download_Cancellation_ReturnsNull()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.TestRepositoryUri);
        var expectedRepositoryDirectory = Path.Combine(_repositoriesDirectory, repositoryUri.AbsolutePath[1..]);
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(0));

        // Act
        var result = _sut.Download(repositoryUri, cts.Token);

        // Assert
        result.Should().BeNull();
        _logger.Should().Log<UserCancelledException>(
            LogLevel.Error,
            $"Unable to clone repository {repositoryUri} to {expectedRepositoryDirectory}");
    }

    [Fact]
    public void Download_CorruptedExistingDirectoryRepository_ReturnsDirectoryRepository()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.TestRepositoryUri);
        var expectedRepositoryDirectory = Path.Combine(_repositoriesDirectory, repositoryUri.AbsolutePath[1..]);
        Directory.CreateDirectory(expectedRepositoryDirectory);

        // Act
        var result = _sut.Download(repositoryUri, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _logger.Should()
            .Log<RepositoryNotFoundException>(
                LogLevel.Warning,
                $"Unable to pull repository {Constants.TestRepositoryUri} to {expectedRepositoryDirectory}")
            .And.Log(LogLevel.Debug, $"Cloning repository {Constants.TestRepositoryUri} in {expectedRepositoryDirectory}");
    }

    [Fact]
    public void Download_EmptyRepository_ReturnsNull()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.EmptyTestRepositoryUri);
        var expectedRepositoryDirectory = Path.Combine(_repositoriesDirectory, repositoryUri.AbsolutePath[1..]);

        // Act
        var result = _sut.Download(repositoryUri, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _logger.Should()
            .Log(LogLevel.Error, $"No valid branch found in {expectedRepositoryDirectory}");
    }
}
