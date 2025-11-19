using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.Logging;
using ScoopSearch.Indexer.Git;
using ScoopSearch.Indexer.Tests.Helpers;

namespace ScoopSearch.Indexer.Tests.Git;

public class GitRepositoryTests : IDisposable
{
    private readonly XUnitLogger<GitRepository> _logger;
    private readonly string _repositoriesDirectory;
    private readonly GitRepositoryProvider _provider;

    public GitRepositoryTests()
    {
        _logger = new XUnitLogger<GitRepository>();

        _repositoriesDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "repositoriesTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_repositoriesDirectory);
        _logger.LogDebug("Repositories root: {RepositoriesDirectory}", _repositoriesDirectory);

        _provider = new GitRepositoryProvider(_logger, _repositoriesDirectory);
    }

    public void Dispose()
    {
        _logger.LogDebug("Deleting repositories root: {RepositoriesDirectory}", _repositoriesDirectory);
        Directory.Delete(_repositoriesDirectory, true);
    }

    [Fact]
    public void Delete_ExistingRepository_Deleted()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.TestRepositoryUri);
        var expectedRepositoryDirectory = Path.Combine(_repositoriesDirectory, repositoryUri.AbsolutePath[1..]);
        var cancellationToken = new CancellationToken();
        var repository = _provider.Download(repositoryUri, cancellationToken)!;

        // Assert
        Directory.Exists(expectedRepositoryDirectory).Should().BeTrue();

        // Act
        repository.Delete();

        // Assert
        Directory.Exists(expectedRepositoryDirectory).Should().BeFalse();
    }

    [Fact]
    public void GetBranchName_Succeeds()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.TestRepositoryUri);
        var cancellationToken = new CancellationToken();
        var repository = _provider.Download(repositoryUri, cancellationToken)!;

        // Act
        var result = repository.GetBranchName();

        // Assert
        result.Should().Be("master");
    }

    [Fact]
    public void Dispose_Succeeds()
    {
        // Arrange
        var repositoryUri = new Uri(Constants.TestRepositoryUri);
        var cancellationToken = new CancellationToken();
        var repository = _provider.Download(repositoryUri, cancellationToken)!;

        // Act + Assert
        repository.Should().BeAssignableTo<IDisposable>().Subject.Dispose();
    }

    [Fact]
    public void GetItemsFromIndex_ReturnsEntries()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var repository = _provider.Download(new Uri(Constants.TestRepositoryUri), cancellationToken)!;

        // Act
        var result = repository.GetFilesFromIndex();

        // Assert
        result.Should().HaveCount(7);
    }

    [Fact]
    public async Task ReadContentAsync_NonExistentEntry_Throws()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var repository = _provider.Download(new Uri(Constants.TestRepositoryUri), cancellationToken)!;

        // Act
        var result = async () => await repository.ReadContentAsync("foo", cancellationToken);

        // Assert
        await result.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ReadContentAsync_ExistentEntry_ReturnsContent()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var repository = _provider.Download(new Uri(Constants.TestRepositoryUri), cancellationToken)!;

        // Act
        var result = async () => await repository.ReadContentAsync("kaxaml.json", cancellationToken);

        // Assert
        var taskResult = await result.Should().NotThrowAsync();
        JsonSerializer.Deserialize<object?>(taskResult.Subject).Should().NotBeNull();
    }

    [Theory]
    [MemberData(nameof(GetCommitsCacheTestCases))]
    public async Task GetCommitsCacheAsync_ReturnsExpectedFilesAndCommits(string repositoryUri, Predicate<string> filter, int expectedFiles, int expectedCommits)
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var repository = _provider.Download(new Uri(repositoryUri), cancellationToken)!;

        // Act
        var result = async () => await repository.GetCommitsCacheAsync(filter, cancellationToken);

        // Assert
        var taskResult = await result.Should().NotThrowAsync();
        taskResult.Subject.Should().HaveCount(expectedFiles)
            .And.Subject.SelectMany(kv => kv.Value).DistinctBy(commitInfo => commitInfo.Sha).Should().HaveCount(expectedCommits);
    }

    public static IEnumerable<object[]> GetCommitsCacheTestCases()
    {
        // repository, filter, expected files, expected commits
        yield return new object[] { Constants.TestRepositoryUri, new Predicate<string>(_ => true), 14, 39 };
        yield return new object[] { Constants.TestRepositoryUri, new Predicate<string>(_ => false), 0, 0 };
        yield return new object[] { Constants.TestRepositoryUri, new Predicate<string>(filePath => filePath.EndsWith(".json")), 11, 30 };
    }

    [Theory]
    [InlineData(Constants.TestRepositoryUri, 1, 5)]
    [InlineData("https://github.com/niheaven/scoop-sysinternals", 1, 70)]
    [InlineData("https://github.com/ScoopInstaller/Extras", 20, 1_900)]
    public async Task GetCommitsCacheAsync_BuildCache_Succeeds(string repositoryUri, double maxSeconds, int minimalManifestsCount)
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var repository = _provider.Download(new Uri(repositoryUri), cancellationToken)!;
        bool IsManifestFile(string filePath) => Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase);

        // Act
        var result = async () => await repository.GetCommitsCacheAsync(IsManifestFile, cancellationToken);

        // Assert
        var taskResult = await result.Should().CompleteWithinAsync(maxSeconds.Seconds());
        taskResult.Subject.Should().HaveCountGreaterThan(minimalManifestsCount);
    }
}
