using System.Reflection;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

        var executionContextOptions = ServiceDescriptor.Singleton<IOptions<ExecutionContextOptions>>(
            serviceProvider =>
                new OptionsWrapper<ExecutionContextOptions>(
                    new ExecutionContextOptions
                    {
                        AppDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                    }));

        var host = new HostBuilder()
            .ConfigureWebJobs(builder => builder
                .UseWebJobsStartup(typeof(Startup), new WebJobsBuilderContext(), startupLoggerFactoryMock.Object))

            .ConfigureServices(services => services
                .Replace(executionContextOptions))
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

