using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ScoopSearch.Indexer.Buckets;
using ScoopSearch.Indexer.Buckets.Sources;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.GitHub;
using ScoopSearch.Indexer.Tests.Helpers;

namespace ScoopSearch.Indexer.Tests.Buckets.Sources;

public class GitHubBucketsSourceTests
{
    private readonly Mock<IGitHubClient> _gitHubClientMock;
    private readonly GitHubOptions _gitHubOptions;
    private readonly XUnitLogger<GitHubBucketsSource> _logger;
    private readonly GitHubBucketsSource _sut;

    public GitHubBucketsSourceTests(ITestOutputHelper testOutputHelper)
    {
        _gitHubClientMock = new Mock<IGitHubClient>();
        _gitHubOptions = new GitHubOptions();
        _logger = new XUnitLogger<GitHubBucketsSource>(testOutputHelper);
        _sut = new GitHubBucketsSource(_gitHubClientMock.Object, new OptionsWrapper<GitHubOptions>(_gitHubOptions), _logger);
    }

    [Fact]
    public async Task GetBucketsAsync_InvalidQueries_ReturnsEmpty()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        _gitHubOptions.BucketsSearchQueries = null;

        // Act
        var result = await _sut.GetBucketsAsync(cancellationToken).ToArrayAsync(cancellationToken);

        // Arrange
        result.Should().BeEmpty();
        _logger.Should().Log(LogLevel.Warning, "No buckets search queries found in configuration");
    }

    [Fact]
    public async Task GetBucketsAsync_Succeeds()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var input = new (string[] queries, GitHubRepo[] repos)[]
        {
            (new[] { "foo", "bar" }, new[] { Faker.CreateGitHubRepo().Generate() }),
            (new[] { "bar", "foo" }, new[] { Faker.CreateGitHubRepo().Generate() }),
        };
        _gitHubOptions.BucketsSearchQueries = input.Select(x => x.queries).ToArray();
        _gitHubClientMock.Setup(x => x.SearchRepositoriesAsync(input[0].queries, cancellationToken)).Returns(input[0].repos.ToAsyncEnumerable());
        _gitHubClientMock.Setup(x => x.SearchRepositoriesAsync(input[1].queries, cancellationToken)).Returns(input[1].repos.ToAsyncEnumerable());

        // Act
        var result = await _sut.GetBucketsAsync(cancellationToken).ToArrayAsync(cancellationToken);

        // Arrange
        result.Should().BeEquivalentTo(
            input.SelectMany(x => x.repos),
            options => options
                .WithMapping<Bucket>(x => x.HtmlUri, y => y.Uri));
    }
}
