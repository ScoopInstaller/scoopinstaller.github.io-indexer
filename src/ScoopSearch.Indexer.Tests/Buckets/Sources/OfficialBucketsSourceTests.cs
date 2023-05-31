using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using ScoopSearch.Indexer.Buckets;
using ScoopSearch.Indexer.Buckets.Providers;
using ScoopSearch.Indexer.Buckets.Sources;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.Tests.Helpers;
using Xunit.Abstractions;
using Faker = ScoopSearch.Indexer.Tests.Helpers.Faker;

namespace ScoopSearch.Indexer.Tests.Buckets.Sources;

public class OfficialBucketsSourceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IBucketsProvider> _bucketsProviderMock;
    private readonly BucketsOptions _bucketsOptions;
    private readonly XUnitLogger<OfficialBucketsSource> _logger;
    private readonly OfficialBucketsSource _sut;

    public OfficialBucketsSourceTests(ITestOutputHelper testOutputHelper)
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _bucketsProviderMock = new Mock<IBucketsProvider>();
        _bucketsOptions = new BucketsOptions();
        _logger = new XUnitLogger<OfficialBucketsSource>(testOutputHelper);

        _sut = new OfficialBucketsSource(
            _httpClientFactoryMock.Object,
            new[] {_bucketsProviderMock.Object },
            new OptionsWrapper<BucketsOptions>(_bucketsOptions),
            _logger);
    }

    [Fact]
    public async void GetBucketsAsync_InvalidUri_ReturnsEmpty()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        _bucketsOptions.OfficialBucketsListUrl = null;

        // Act
        var result = await _sut.GetBucketsAsync(cancellationToken).ToArrayAsync(cancellationToken);

        // Arrange
        result.Should().BeEmpty();
        _logger.Should().Log(LogLevel.Warning, "No official buckets list url found in configuration");
    }

    [Theory]
    [MemberData(nameof(GetBucketsAsyncErrorsTestCases))]
#pragma warning disable xUnit1026
    public async void GetBucketsAsync_InvalidStatusCodeSucceeds<TExpectedException>(HttpStatusCode statusCode, string content, TExpectedException _)
#pragma warning restore xUnit1026
        where TExpectedException : Exception
    {
        // Arrange
        _bucketsOptions.OfficialBucketsListUrl = Faker.CreateUri();
        var cancellationToken = new CancellationToken();
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(x => x.RequestUri == _bucketsOptions.OfficialBucketsListUrl),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage() { StatusCode = statusCode, Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(content))) });
        _httpClientFactoryMock.Setup(x => x.CreateClient("Default")).Returns(new HttpClient(httpMessageHandlerMock.Object));

        // Act
        var result = async () => await _sut.GetBucketsAsync(cancellationToken).ToArrayAsync(cancellationToken);

        // Assert
        await result.Should().ThrowAsync<TExpectedException>();
    }

    public static IEnumerable<object[]> GetBucketsAsyncErrorsTestCases()
    {
        yield return new object[] { HttpStatusCode.NotFound, $"url", new HttpRequestException() };
        yield return new object[] { HttpStatusCode.OK, "", new JsonException() };
        yield return new object[] { HttpStatusCode.OK, $"foo", new JsonException() };
    }

    [Theory]
    [MemberData(nameof(GetBucketsAsyncTestCases))]
    public async void GetBucketsAsync_Succeeds(string content, string repositoryUri, bool isCompatible, bool expectedBucket)
    {
        // Arrange
        _bucketsOptions.OfficialBucketsListUrl = Faker.CreateUri();
        var cancellationToken = new CancellationToken();
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClientFactoryMock.Setup(x => x.CreateClient("Default")).Returns(new HttpClient(httpMessageHandlerMock.Object));

        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(x => x.RequestUri == _bucketsOptions.OfficialBucketsListUrl),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage() { StatusCode = HttpStatusCode.OK, Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(content))) });
        Bucket bucket = new Bucket(new Uri(repositoryUri), 123);
        _bucketsProviderMock.Setup(x => x.IsCompatible(new Uri(repositoryUri))).Returns(isCompatible);
        _bucketsProviderMock.Setup(x => x.GetBucketAsync(new Uri(repositoryUri), cancellationToken)).ReturnsAsync(bucket);

        // Act
        var result = await _sut.GetBucketsAsync(cancellationToken).ToArrayAsync(cancellationToken);

        // Arrange
        result.Should().HaveCount(expectedBucket ? 1 : 0);
        if (expectedBucket)
        {
            result.Should().BeEquivalentTo(new[] { bucket });
        }
    }

    public static IEnumerable<object[]> GetBucketsAsyncTestCases()
    {
        var url = Faker.CreateUrl();
        yield return new object[] { $@"{{ }}", url, false, false };
        yield return new object[] { $@"{{ }}", url, true, false };
        yield return new object[] { $@"{{ ""foo"": ""{url}"" }}", url, false, false };
        yield return new object[] { $@"{{ ""foo"": ""{url}"" }}", url, true, true };
    }
}
