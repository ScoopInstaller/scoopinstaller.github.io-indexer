using System.Globalization;
using System.Net;
using CsvHelper;
using CsvHelper.Configuration;
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
using Faker = ScoopSearch.Indexer.Tests.Helpers.Faker;
using MissingFieldException = CsvHelper.MissingFieldException;

namespace ScoopSearch.Indexer.Tests.Buckets.Sources;

public class ManualBucketsListSourceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IBucketsProvider> _bucketsProviderMock;
    private readonly BucketsOptions _bucketsOptions;
    private readonly XUnitLogger<ManualBucketsListSource> _logger;
    private readonly ManualBucketsListSource _sut;

    public ManualBucketsListSourceTests(ITestOutputHelper testOutputHelper)
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _bucketsProviderMock = new Mock<IBucketsProvider>();
        _bucketsOptions = new BucketsOptions();
        _logger = new XUnitLogger<ManualBucketsListSource>(testOutputHelper);

        _sut = new ManualBucketsListSource(
            _httpClientFactoryMock.Object,
            new[] {_bucketsProviderMock.Object },
            new OptionsWrapper<BucketsOptions>(_bucketsOptions),
            _logger);
    }

    [Fact]
    public async Task GetBucketsAsync_InvalidUri_ReturnsEmpty()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        _bucketsOptions.ManualBucketsListUrl = null;

        // Act
        var result = await _sut.GetBucketsAsync(cancellationToken).ToArrayAsync(cancellationToken);

        // Arrange
        result.Should().BeEmpty();
        _logger.Should().Log(LogLevel.Warning, "No buckets list url found in configuration");
    }

    [Theory]
    [MemberData(nameof(GetBucketsAsyncErrorsTestCases))]
#pragma warning disable xUnit1026
    public async Task GetBucketsAsync_InvalidStatusCodeSucceeds<TExpectedException>(HttpStatusCode statusCode, string content, TExpectedException _)
#pragma warning restore xUnit1026
        where TExpectedException : Exception
    {
        // Arrange
        _bucketsOptions.ManualBucketsListUrl = Faker.CreateUri();
        var cancellationToken = new CancellationToken();
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(x => x.RequestUri == _bucketsOptions.ManualBucketsListUrl),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage() { StatusCode = statusCode, Content = new StringContent(content) });
        _httpClientFactoryMock.Setup(x => x.CreateClient("Default")).Returns(new HttpClient(httpMessageHandlerMock.Object));

        // Act
        var result = async () => await _sut.GetBucketsAsync(cancellationToken).ToArrayAsync(cancellationToken);

        // Assert
        await result.Should().ThrowAsync<TExpectedException>();
    }

    public static TheoryData<HttpStatusCode, string, Exception> GetBucketsAsyncErrorsTestCases() =>
        new()
        {
            { HttpStatusCode.NotFound, "url", new HttpRequestException() },
            { HttpStatusCode.OK, "", new ReaderException(new CsvContext(new CsvConfiguration(CultureInfo.InvariantCulture))) },
            { HttpStatusCode.OK, $"foo{Environment.NewLine}{Faker.CreateUrl()}", new MissingFieldException(new CsvContext(new CsvConfiguration(CultureInfo.InvariantCulture))) },
        };
    

    [Theory]
    [MemberData(nameof(GetBucketsAsyncTestCases))]
    public async Task GetBucketsAsync_Succeeds(string content, string repositoryUri, bool isCompatible, bool expectedBucket)
    {
        // Arrange
        _bucketsOptions.ManualBucketsListUrl = Faker.CreateUri();
        var cancellationToken = new CancellationToken();
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClientFactoryMock.Setup(x => x.CreateClient("Default")).Returns(new HttpClient(httpMessageHandlerMock.Object));

        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(x => x.RequestUri == _bucketsOptions.ManualBucketsListUrl),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage() { StatusCode = HttpStatusCode.OK, Content = new StringContent(content) });
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

    public static TheoryData<string, string, bool, bool> GetBucketsAsyncTestCases()
    {
        var data = new TheoryData<string, string, bool, bool>();
        data.Add("url", Faker.CreateUrl(), true, false);
        var url = Faker.CreateUrl();
        data.Add($"url{Environment.NewLine}{url}", url, false, false);
        data.Add($"url{Environment.NewLine}{url}", url, true, true);
        data.Add($"url{Environment.NewLine}{url}.git", url, true, true);

        return data;
    }
}
