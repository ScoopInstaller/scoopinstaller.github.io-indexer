using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ScoopSearch.Indexer.Buckets;
using ScoopSearch.Indexer.Buckets.Sources;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.GitLab;
using ScoopSearch.Indexer.Tests.Helpers;
using Xunit.Abstractions;

namespace ScoopSearch.Indexer.Tests.Buckets.Sources;

public class GitLabBucketsSourceTests
{
    private readonly Mock<IGitLabClient> _gitLabClientMock;
    private readonly GitLabOptions _gitLabOptions;
    private readonly XUnitLogger<GitLabBucketsSource> _logger;
    private readonly GitLabBucketsSource _sut;

    public GitLabBucketsSourceTests(ITestOutputHelper testOutputHelper)
    {
        _gitLabClientMock = new Mock<IGitLabClient>();
        _gitLabOptions = new GitLabOptions();
        _logger = new XUnitLogger<GitLabBucketsSource>(testOutputHelper);
        _sut = new GitLabBucketsSource(_gitLabClientMock.Object, new OptionsWrapper<GitLabOptions>(_gitLabOptions), _logger);
    }

    [Fact]
    public async void GetBucketsAsync_InvalidQueries_ReturnsEmpty()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        _gitLabOptions.BucketsSearchQueries = null;

        // Act
        var result = await _sut.GetBucketsAsync(cancellationToken).ToArrayAsync(cancellationToken);

        // Arrange
        result.Should().BeEmpty();
        _logger.Should().Log(LogLevel.Warning, "No buckets search queries found in configuration");
    }

    [Fact]
    public async void GetBucketsAsync_Succeeds()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var input = new (string query, GitLabRepo[] repos)[]
        {
            ("foo", new[] { Faker.CreateGitLabRepo().Generate() }),
            ("bar", new[] { Faker.CreateGitLabRepo().Generate() }),
        };
        _gitLabOptions.BucketsSearchQueries = input.Select(x => x.query).ToArray();
        _gitLabClientMock.Setup(x => x.SearchRepositoriesAsync(input[0].query, cancellationToken)).Returns(input[0].repos.ToAsyncEnumerable());
        _gitLabClientMock.Setup(x => x.SearchRepositoriesAsync(input[1].query, cancellationToken)).Returns(input[1].repos.ToAsyncEnumerable());

        // Act
        var result = await _sut.GetBucketsAsync(cancellationToken).ToArrayAsync(cancellationToken);

        // Arrange
        result.Should().BeEquivalentTo(
            input.SelectMany(x => x.repos),
            options => options
                .WithMapping<Bucket>(x => x.WebUrl, y => y.Uri));
    }
}
