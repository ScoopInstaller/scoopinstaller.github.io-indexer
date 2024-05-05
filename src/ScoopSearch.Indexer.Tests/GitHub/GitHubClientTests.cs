using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ScoopSearch.Indexer.GitHub;
using ScoopSearch.Indexer.Tests.Helpers;
using Xunit.Abstractions;

namespace ScoopSearch.Indexer.Tests.GitHub;

[SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments")]
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
    public async Task GetRepositoryAsync_InvalidRepo_ReturnsNull(string input)
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

    [Theory]
    [InlineData("http://example.invalid/foo/bar")]
    public async Task GetRepositoryAsync_InvalidDomain_Throws(string input)
    {
        // Arrange
        var uri = new Uri(input);
        var cancellationToken = new CancellationToken();

        // Act
        var result = () => _sut.GetRepositoryAsync(uri, cancellationToken);

        // Assert
        await result.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetRepositoryAsync_NonExistentRepo_ReturnsNull()
    {
        // Arrange
        var uri = new Uri(Constants.NonExistentTestRepositoryUri);
        var cancellationToken = new CancellationToken();

        // Act
        var result = await _sut.GetRepositoryAsync(uri, cancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRepositoryAsync_RedirectedRepo_ReturnsNull()
    {
        // Arrange
        var uri = new Uri("https://github.com/MCOfficer/scoop-nirsoft");
        var cancellationToken = new CancellationToken();

        // Act
        var result = await _sut.GetRepositoryAsync(uri, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.HtmlUri.Should().Be("https://github.com/ScoopInstaller/Nirsoft");
    }

    [Theory]
    [InlineData("https://github.com/ScoopInstaller/Main", 1000)]
    [InlineData("https://github.com/ScoopInstaller/Extras", 1500)]
    public async Task GetRepositoryAsync_ValidRepo_ReturnsGitHubRepo(string input, int expectedMinimumStars)
    {
        // Arrange
        var uri = new Uri(input);
        var cancellationToken = new CancellationToken();

        // Act
        var result = await _sut.GetRepositoryAsync(uri, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.HtmlUri.Should().Be(uri);
        result.Stars.Should().BeGreaterThan(expectedMinimumStars, "because official repo should have a large amount of stars");
    }

    [Theory]
    [InlineData(new object[] { new string[0] })]
    [InlineData(new object[] { new[] { "" } })]
    [InlineData(new object[] { new[] { "&&==" } })]
    public async Task SearchRepositoriesAsync_InvalidQueryUrl_Throws(string[] input)
    {
        // Arrange + Act
        var cancellationToken = new CancellationToken();
        try
        {
            await _sut.SearchRepositoriesAsync(input, cancellationToken).ToArrayAsync(cancellationToken);
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
    public async Task SearchRepositoriesAsync_ValidQuery_ReturnsSearchResults(string[] input)
    {
        // Arrange + Act
        var cancellationToken = new CancellationToken();
        var result = await _sut.SearchRepositoriesAsync(input, cancellationToken).ToArrayAsync(cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should()
            .BeGreaterThan(0, "because there should be at least 1 result")
            .And.BeLessThan(900, "because there should be less than 900 results. If it returns more than 900, the date condition should be updated");
    }
}
