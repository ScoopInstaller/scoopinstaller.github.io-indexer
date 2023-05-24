using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ScoopSearch.Indexer.GitHub;
using ScoopSearch.Indexer.Tests.Helpers;
using Xunit.Abstractions;

namespace ScoopSearch.Indexer.Tests.GitHub;

public class GitHubClientTests : IClassFixture<HostFixture>
{
    private readonly GitHubClient _sut;

    public GitHubClientTests(HostFixture hostFixture, ITestOutputHelper testOutputHelper)
    {
        hostFixture.Configure(testOutputHelper);
        var logger = new XUnitLogger<GitHubClient>(testOutputHelper);

        _sut = new GitHubClient(hostFixture.Instance.Services.GetRequiredService<IHttpClientFactory>(), logger);
    }

    [Theory]
    [InlineData("http://example.com/foo/bar")]
    public async void GetRepositoryAsync_InvalidRepo_ReturnsNull(string input)
    {
        // Arrange
        var uri = new Uri(input);

        // Act
        var result = () => _sut.GetRepositoryAsync(uri, CancellationToken.None);

        // Assert
        var taskResult = await result.Should().NotThrowAsync();
        taskResult.Subject.Should().BeNull();
    }

    [Theory]
    [InlineData("http://example.invalid/foo/bar")]
    public async void GetRepositoryAsync_InvalidDomain_Throws(string input)
    {
        // Arrange
        var uri = new Uri(input);

        // Act
        var result = () => _sut.GetRepositoryAsync(uri, CancellationToken.None);

        // Assert
        await result.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async void GetRepositoryAsync_NonExistentRepo_ReturnsNull()
    {
        // Arrange
        var uri = new Uri(Constants.NonExistentTestRepositoryUri);

        // Act
        var result = await _sut.GetRepositoryAsync(uri, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("https://github.com/ScoopInstaller/Main", 1000)]
    [InlineData("https://github.com/ScoopInstaller/Extras", 1500)]
    public async void GetRepositoryAsync_ValidRepo_ReturnsGitHubRepo(string input, int expectedMinimumStars)
    {
        // Arrange
        var uri = new Uri(input);

        // Act
        var result = await _sut.GetRepositoryAsync(uri, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.HtmlUri.Should().Be(uri);
        result.Stars.Should().BeGreaterThan(expectedMinimumStars, "because official repo should have a large amount of stars");
    }

    [Theory]
    [InlineData(new object[] { new string[0] })]
    [InlineData(new object[] { new[] { "" } })]
    [InlineData(new object[] { new[] { "&&==" } })]
    public async void SearchRepositoriesAsync_InvalidQueryUrl_Throws(string[] input)
    {
        // Arrange + Act
        try
        {
            await _sut.SearchRepositoriesAsync(input, CancellationToken.None).ToArrayAsync();
            Assert.Fail("Should have thrown");
        }
        catch (AggregateException ex)
        {
            // Assert
            ex.InnerException.Should().BeOfType<HttpRequestException>();
            return;
        }

        Assert.Fail("Should have thrown an AggregateException");
    }

    [Theory]
    [InlineData(new object[] { new[] { "scoop-bucket", "created:>2023-01-01" } })]
    [InlineData(new object[] { new[] { "scoop+bucket", "created:>2023-01-01" } })]
    public async void SearchRepositoriesAsync_ValidQuery_ReturnsSearchResults(string[] input)
    {
        // Arrange + Act
        var result = await _sut.SearchRepositoriesAsync(input, CancellationToken.None).ToArrayAsync();

        // Assert
        result.Should().NotBeNull();
        result.Length.Should()
            .BeGreaterThan(0, "because there should be at least 1 result")
            .And.BeLessThan(900, "because there should be less than 900 results. If it returns more than 900, the date condition should be updated");
    }
}
