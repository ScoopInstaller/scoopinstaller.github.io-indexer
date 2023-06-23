using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ScoopSearch.Indexer.GitLab;
using ScoopSearch.Indexer.Tests.Helpers;
using Xunit.Abstractions;

namespace ScoopSearch.Indexer.Tests.GitLab;

public class GitLabClientTests : IClassFixture<HostFixture>
{
    private readonly GitLabClient _sut;

    public GitLabClientTests(HostFixture hostFixture, ITestOutputHelper testOutputHelper)
    {
        hostFixture.Configure(testOutputHelper);
        var logger = new XUnitLogger<GitLabClient>(testOutputHelper);

        _sut = new GitLabClient(hostFixture.Instance.Services.GetRequiredService<IHttpClientFactory>(), logger);
    }

    [Theory]
    [InlineData("http://example.com/foo/bar")]
    public async void GetRepositoryAsync_InvalidRepo_ReturnsNull(string input)
    {
        // Arrange
        var uri = new Uri(input);
        var cancellationToken = new CancellationToken();

        // Act
        var result = () => _sut.GetRepositoryAsync(uri, cancellationToken);

        // Assert
        var taskResult = await result.Should().NotThrowAsync();
        taskResult.Subject.Should().BeNull();
    }

    [Fact]
    public async void GetRepositoryAsync_NonExistentRepo_ReturnsNull()
    {
        // Arrange
        var uri = new Uri(Constants.NonExistentTestRepositoryUri);
        var cancellationToken = new CancellationToken();

        // Act
        var result = await _sut.GetRepositoryAsync(uri, cancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("https://gitlab.com/aknackd/scoop-nightly-neovim", 0)]
    [InlineData("https://gitlab.com/jbmorice/scoop_bucket", 1)]
    public async void GetRepositoryAsync_ValidRepo_ReturnsGitLabRepo(string input, int expectedMinimumStars)
    {
        // Arrange
        var uri = new Uri(input);
        var cancellationToken = new CancellationToken();

        // Act
        var result = await _sut.GetRepositoryAsync(uri, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.WebUrl.Should().Be(uri);
        result.Stars.Should().BeGreaterOrEqualTo(expectedMinimumStars, "because official repo should have a large amount of stars");
    }

    [Theory]
    [InlineData("&&==")]
    public async void SearchRepositoriesAsync_InvalidQueryUrl_Throws(string input)
    {
        // Arrange
        var cancellationToken = new CancellationToken();

        // Act
        var result = await _sut.SearchRepositoriesAsync(input, cancellationToken).ToArrayAsync(cancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("scoop-bucket")]
    [InlineData("scoop")]
    public async void SearchRepositoriesAsync_ValidQuery_ReturnsSearchResults(string input)
    {
        // Arrange
        var cancellationToken = new CancellationToken();

        // Act
        var result = await _sut.SearchRepositoriesAsync(input, cancellationToken).ToArrayAsync(cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should()
            .BeGreaterThan(0, "because there should be at least 1 result")
            .And.BeLessThan(90, "because there should be less than 90 results. If it returns more than 90, the pagination should be implemented (max items per page is 100)");
    }
}
