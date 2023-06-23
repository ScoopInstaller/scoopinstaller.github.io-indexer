using FluentAssertions;
using Moq;
using ScoopSearch.Indexer.Buckets.Providers;
using ScoopSearch.Indexer.GitLab;
using ScoopSearch.Indexer.Tests.Helpers;

namespace ScoopSearch.Indexer.Tests.Buckets.Providers;

public class GitLabBucketsProviderTests
{
    private readonly Mock<IGitLabClient> _gitLabClientMock;
    private readonly GitLabBucketsProvider _sut;

    public GitLabBucketsProviderTests()
    {
        _gitLabClientMock = new Mock<IGitLabClient>();
        _sut = new GitLabBucketsProvider(_gitLabClientMock.Object);
    }

    [Theory]
    [InlineData("http://foo/bar", false)]
    [InlineData("https://foo/bar", false)]
    [InlineData("http://www.google.fr/foo", false)]
    [InlineData("https://www.google.fr/foo", false)]
    [InlineData("http://gitlab.com", true)]
    [InlineData("https://gitlab.com", true)]
    [InlineData("http://www.gitlab.com", true)]
    [InlineData("https://www.gitlab.com", true)]
    [InlineData("http://www.GitLab.com", true)]
    [InlineData("https://www.GitLab.com", true)]
    [InlineData("https://www.gitlab.com/foo/bar", true)]
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
        var gitLabRepo = Faker.CreateGitLabRepo().Generate();
        _gitLabClientMock.Setup(x => x.GetRepositoryAsync(uri, cancellationToken)).ReturnsAsync(gitLabRepo);

        // Act
        var result = await _sut.GetBucketAsync(uri, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Uri.Should().Be(gitLabRepo.WebUrl);
        result.Stars.Should().Be(gitLabRepo.Stars);
    }

    [Fact]
    public async void GetBucketAsync_InvalidRepo_ReturnsNull()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var uri = Faker.CreateUri();
        _gitLabClientMock.Setup(x => x.GetRepositoryAsync(uri, cancellationToken)).ReturnsAsync((GitLabRepo?)null);

        // Act
        var result = await _sut.GetBucketAsync(uri, cancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
