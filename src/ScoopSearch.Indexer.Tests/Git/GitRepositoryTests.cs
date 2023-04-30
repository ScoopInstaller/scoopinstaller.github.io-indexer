using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.Logging;
using ScoopSearch.Indexer.Git;
using ScoopSearch.Indexer.Tests.Helpers;
using Xunit.Abstractions;

namespace ScoopSearch.Indexer.Tests.Git;

public class GitRepositoryTests : IDisposable
{
    private readonly XUnitLogger<GitRepository> _logger;
    private readonly string _repositoriesDirectory;
    private readonly GitRepositoryProvider _provider;

    public GitRepositoryTests(ITestOutputHelper testOutputHelper)
    {
        _logger = new XUnitLogger<GitRepository>(testOutputHelper);

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
        var repository = _provider.Download(repositoryUri, CancellationToken.None)!;

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
        var repository = _provider.Download(repositoryUri, CancellationToken.None)!;

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
        var repository = _provider.Download(repositoryUri, CancellationToken.None)!;

        // Act + Assert
        repository.Should().BeAssignableTo<IDisposable>().Subject.Dispose();
    }

    [Fact]
    public void GetItemsFromIndex_ReturnsEntries()
    {
        // Arrange
        var repository = _provider.Download(new Uri(Constants.TestRepositoryUri), CancellationToken.None)!;

        // Act
        var result = repository.GetFilesFromIndex();

        // Assert
        result.Should().HaveCount(7);
    }

    [Fact]
    public void ReadContent_NonExistentEntry_Throws()
    {
        // Arrange
        var repository = _provider.Download(new Uri(Constants.TestRepositoryUri), CancellationToken.None)!;

        // Act
        Action act = () => repository.ReadContent("foo");

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ReadContent_ExistentEntry_ReturnsContent()
    {
        // Arrange
        var repository = _provider.Download(new Uri(Constants.TestRepositoryUri), CancellationToken.None)!;

        // Act
        var result = repository.ReadContent("kaxaml.json");

        // Assert
        result.Should().NotBeNull();
        JsonSerializer.Deserialize<object?>(result).Should().NotBeNull();
    }

    [Theory]
    [MemberData(nameof(GetCommitsCacheTestCases))]
    public void GetCommitsCache_ReturnsExpectedFilesAndCommits(string repositoryUri, Predicate<string> filter, int expectedFiles, int expectedCommits)
    {
        // Arrange
        var repository = _provider.Download(new Uri(repositoryUri), CancellationToken.None)!;

        // Act
        var result = repository.GetCommitsCache(filter, CancellationToken.None);

        // Assert
        result.Should().HaveCount(expectedFiles);
        result.SelectMany(_ => _.Value).DistinctBy(_ => _.Sha).Should().HaveCount(expectedCommits);
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
        var repository = _provider.Download(new Uri(repositoryUri), CancellationToken.None)!;
        bool IsManifestFile(string filePath) => Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase);

        // Act
        IReadOnlyDictionary<string, IReadOnlyCollection<CommitInfo>>? result = null;
        Action act = () => result = repository.GetCommitsCache(IsManifestFile, CancellationToken.None);

        // Assert
        act.ExecutionTime().Should().BeLessThan(maxSeconds.Seconds());
        result.Should().HaveCountGreaterThan(minimalManifestsCount);
    }
}
