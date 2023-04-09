using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using ScoopSearch.Functions.Tests.Helpers;
using Xunit.Abstractions;

namespace ScoopSearch.Functions.Tests;

public class HostFixture : IDisposable
{
    private const LogLevel MinimumLogLevel = LogLevel.Debug;

    private readonly Lazy<IHost> _lazyHost;

    private ITestOutputHelper? _testOutputHelper;

    public HostFixture()
    {
        _lazyHost = new Lazy<IHost>(CreateHost);
    }

    public IHost Host => _lazyHost.Value;

    public void Dispose()
    {
        if (_lazyHost.IsValueCreated)
        {
            _lazyHost.Value.Dispose();
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

        var startupLoggerFactoryMock = new Mock<ILoggerFactory>();
        startupLoggerFactoryMock
            .Setup(_ => _.CreateLogger(It.IsAny<string>()))
            .Returns<string>(loggerName => new XUnitLogger(loggerName, _testOutputHelper));

        var host = new HostBuilder()
            .ConfigureWebJobs(builder => builder
                 .UseWebJobsStartup(typeof(Startup), new WebJobsBuilderContext(), startupLoggerFactoryMock.Object))
            .ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddJsonFile(Path.Combine(_.HostingEnvironment.ContentRootPath, "settings.json"), optional: false, reloadOnChange: false);
            })
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

        return host;
    }
}

