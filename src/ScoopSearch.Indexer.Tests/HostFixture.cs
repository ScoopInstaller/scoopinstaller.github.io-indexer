using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.Tests.Helpers;
using Xunit.Abstractions;

namespace ScoopSearch.Indexer.Tests;

public class HostFixture : IDisposable
{
    private const LogLevel MinimumLogLevel = LogLevel.Debug;

    private readonly Lazy<IHost> _lazyInstance;

    private ITestOutputHelper? _testOutputHelper;

    public HostFixture()
    {
        _lazyInstance = new Lazy<IHost>(CreateHost);
    }

    public IHost Instance => _lazyInstance.Value;

    public void Dispose()
    {
        if (_lazyInstance.IsValueCreated)
        {
            _lazyInstance.Value.Dispose();
        }
    }

    public void Configure(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    private IHost CreateHost()
    {
        if (_testOutputHelper == null)
        {
            throw new InvalidOperationException("{nameof(Configure)} must be called before {nameof(CreateHost)}");
        }

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(_ => _.RegisterScoopSearchIndexer())
            .ConfigureLogging((context, builder) =>
            {
                var loggerProviderMock = new Mock<ILoggerProvider>();
                loggerProviderMock
                    .Setup(_ => _.CreateLogger(It.IsAny<string>()))
                    .Returns<string>(loggerName => new XUnitLogger(loggerName, _testOutputHelper));

                builder.AddProvider(loggerProviderMock.Object);
                builder.SetMinimumLevel(MinimumLogLevel);
            })
            .Build();

        host.Services.GetRequiredService<IOptions<GitHubOptions>>().Value.Token
            .Should().NotBeNullOrEmpty("because the GitHub token is required for the tests to run. Add an environment variable Github__Token with a valid token (public_repo scope).");

        return host;
    }
}

