using FluentAssertions;
using Moq;
using ScoopSearch.Indexer.Buckets.Providers;
using ScoopSearch.Indexer.GitHub;
using ScoopSearch.Indexer.Tests.Helpers;

namespace ScoopSearch.Indexer.Tests.Buckets.Providers;

public class GitHubBucketsProviderTests
{
    private readonly Mock<IGitHubClient> _gitHubClientMock;
    private readonly GitHubBucketsProvider _sut;

    public GitHubBucketsProviderTests()
    {
        _gitHubClientMock = new Mock<IGitHubClient>();
        _sut = new GitHubBucketsProvider(_gitHubClientMock.Object);
    }

    [Theory]
    [InlineData("http://foo/bar", false)]
    [InlineData("https://foo/bar", false)]
    [InlineData("http://www.google.fr/foo", false)]
    [InlineData("https://www.google.fr/foo", false)]
    [InlineData("http://github.com", true)]
    [InlineData("https://github.com", true)]
    [InlineData("http://www.github.com", true)]
    [InlineData("https://www.github.com", true)]
    [InlineData("http://www.GitHub.com", true)]
    [InlineData("https://www.GitHub.com", true)]
    public void IsCompatible_Succeeds(string input, bool expectedResult)
    {
        // Arrange
        var uri = new Uri(input);

        // Act
        var result = _sut.IsCompatible(uri);

        // Arrange
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async void GetBucketAsync_ValidRepo_ReturnsBucket()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var uri = Faker.CreateUri();
        var gitHubRepo = Faker.CreateGitHubRepo().Generate();
        _gitHubClientMock.Setup(x => x.GetRepositoryAsync(uri, cancellationToken)).ReturnsAsync(gitHubRepo);

        // Act
        var result = await _sut.GetBucketAsync(uri, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Uri.Should().Be(gitHubRepo.HtmlUri);
        result.Stars.Should().Be(gitHubRepo.Stars);
    }

    [Fact]
    public async void GetBucketAsync_InvalidRepo_ReturnsNull()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var uri = Faker.CreateUri();
        _gitHubClientMock.Setup(x => x.GetRepositoryAsync(uri, cancellationToken)).ReturnsAsync((GitHubRepo?)null);

        // Act
        var result = await _sut.GetBucketAsync(uri, cancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
