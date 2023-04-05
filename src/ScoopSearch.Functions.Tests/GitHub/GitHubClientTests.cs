using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ScoopSearch.Functions.GitHub;
using Xunit.Abstractions;

namespace ScoopSearch.Functions.Tests.GitHub;

public class GitHubClientTests : IClassFixture<HostFixture>
{
    private readonly GitHubClient _sut;

    public GitHubClientTests(HostFixture hostFixture, ITestOutputHelper testOutputHelper)
    {
        hostFixture.Configure(testOutputHelper);

        _sut = new GitHubClient(hostFixture.Host.Services.GetRequiredService<IHttpClientFactory>());
    }

    [Fact]
    public async void GetAsStringAsync_NonExistentUrl_Throws()
    {
        // Arrange
        var uri = new Uri("http://example.invalid/foo/bar");

        // Act
        Func<Task> act = () => _sut.GetAsStringAsync(uri, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async void GetAsStringAsync_OfficialBuckets_ReturnsDictionaryOfBuckets()
    {
        // Arrange
        var bucketsListUri = new Uri("https://raw.githubusercontent.com/lukesampson/scoop/master/buckets.json");

        // Act
        var result = await _sut.GetAsStringAsync(bucketsListUri, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();

        // Act
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, Uri>>(result);

        // Assert
        dictionary.Should().NotBeNull()
            .And.HaveCountGreaterOrEqualTo(10, "because it contains a bunch of Official buckets")
            .And.ContainKey("main", "because it contains the Official main bucket")
            .And.ContainKey("extras", "because it contains the Official extras bucket");
    }

    [Theory]
    [InlineData("https://raw.githubusercontent.com/rasa/scoop-directory/master/exclude.txt")]
    [InlineData("https://raw.githubusercontent.com/rasa/scoop-directory/master/include.txt")]
    public async void GetAsStringAsync_BucketsLists_ReturnsListOfBuckets(string input)
    {
        // Arrange
        var uri = new Uri(input);

        // Act
        var result = await _sut.GetAsStringAsync(uri, CancellationToken.None);

        // Assert
        result.Should().StartWith("url");
        result.Split(Environment.NewLine).Should().HaveCountGreaterThan(10, "because it contains at least 10 buckets");
    }

    [Theory]
    [InlineData("http://example.invalid/foo/bar")]
    [InlineData("http://example.com/foo/bar")]
    public async void GetRepoAsync_InvalidRepo_Throws(string input)
    {
        // Arrange
        var uri = new Uri(input);

        // Act
        Func<Task> act = () => _sut.GetRepoAsync(uri, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .And.Message.Should().Be("The URI must be a GitHub repo URI. (Parameter 'uri')");
    }

    [Fact]
    public async void GetRepoAsync_NonExistentRepo_ReturnsNull()
    {
        // Arrange
        var uri = new Uri(Constants.NonExistentTestRepositoryUri);

        // Act
        var result = await _sut.GetRepoAsync(uri, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("https://github.com/ScoopInstaller/Main", 1000)]
    [InlineData("https://github.com/ScoopInstaller/Extras", 1500)]
    public async void GetRepoAsync_ValidRepo_ReturnsGitHubRepo(string input, int expectedMinimumStars)
    {
        // Arrange
        var uri = new Uri(input);

        // Act
        var result = await _sut.GetRepoAsync(uri, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.HtmlUri.Should().Be(uri);
        result!.Stars.Should().BeGreaterThan(expectedMinimumStars, "because official repo should have a large amount of stars");
    }

    [Theory]
    [InlineData("http://example.invalid/foo/bar")]
    [InlineData("http://example.com/foo/bar")]
    [InlineData("https://github.com/foo/bar")]
    [InlineData("https://api.github.com/search/repositories?q")]
    public async void GetSearchResultsAsync_InvalidQueryUrl_Throws(string input)
    {
        // Arrange
        var uri = new Uri(input);

        // Act
        Func<Task> act = () => _sut.GetSearchResultsAsync(uri, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Theory]
    [InlineData("https://api.github.com/search/repositories?q=scoop-bucket+created:>2023-01-01")]
    [InlineData("https://api.github.com/search/repositories?q=scoop+bucket+created:>2023-01-01")]
    public async void GetSearchResultsAsync_ValidQuery_ReturnsSearchResults(string input)
    {
        // Arrange
        var uri = new Uri(input);

        // Act
        var result = await _sut.GetSearchResultsAsync(uri, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should()
            .BeGreaterThan(0, "because there should be at least 1 result")
            .And.BeLessThan(900, "because there should be less than 900 results. If it returns more than 900, the date condition should be updated");
        result.Items.Should().NotBeEmpty();
    }

    [Theory]
    [CombinatorialData]
    public async void SendAsync_NonExistentUrl_Throws(bool followRedirects)
    {
        // Arrange
        var uri = new Uri("http://example.invalid/foo/bar");
        var httpRequestMessage = new HttpRequestMessage(HttpMethod.Head, uri);

        // Act
        Func<Task> act = () => _sut.SendAsync(httpRequestMessage, followRedirects, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Theory]
    [InlineData("https://github.com/okibcn/Bucket.git")]
    [InlineData("https://github.com/01walid/it-scoop.git")]
    public async void SendAsync_DontFollowRedirection_Succeeds(string input)
    {
        // Arrange
        var uri = new Uri(input);
        var httpRequestMessage = new HttpRequestMessage(HttpMethod.Head, uri);

        // Act
        var result = await _sut.SendAsync(httpRequestMessage, false, CancellationToken.None);

        // Assert
        result.Should().BeRedirection();
    }

    [Theory]
    [InlineData("https://github.com/okibcn/Bucket.git")]
    [InlineData("https://github.com/01walid/it-scoop.git")]
    public async void SendAsync_FollowRedirection_Succeeds(string input)
    {
        // Arrange
        var uri = new Uri(input);
        var httpRequestMessage = new HttpRequestMessage(HttpMethod.Head, uri);

        // Act
        var result = await _sut.SendAsync(httpRequestMessage, true, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
    }
}
