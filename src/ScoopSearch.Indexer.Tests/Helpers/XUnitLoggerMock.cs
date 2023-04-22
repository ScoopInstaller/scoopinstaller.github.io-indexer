using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace ScoopSearch.Functions.Tests.Helpers;

internal class XUnitLoggerMock<T> : Mock<ILogger<T>>
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly string _loggerName;

    public XUnitLoggerMock(ITestOutputHelper testOutputHelper, string? loggerName = null)
    {
        _testOutputHelper = testOutputHelper;
        _loggerName = loggerName ?? typeof(T).Name;

        Setup(logger => logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(Log);

        Setup(logger => logger.IsEnabled(It.IsAny<LogLevel>()))
            .Returns(true);
        Setup(logger => logger.BeginScope(It.IsAny<It.IsAnyType>()))
            .Returns<object>(state => Disposable.Create(
                () => _testOutputHelper.WriteLine($"{DateTime.Now:u} | ==> | {_loggerName} | {state}"),
                () => _testOutputHelper.WriteLine($"{DateTime.Now:u} | <== | {_loggerName} | {state}")));
    }

    private void Log(LogLevel logLevel, EventId eventId, object state, Exception? exception, Delegate formatter)
    {
        var formattedLogLevel = logLevel.ToString().ToUpper()[..3];
        var message = formatter.DynamicInvoke(state, exception) as string;
        var formattedMessage = $"{DateTime.Now:u} | {formattedLogLevel} | {_loggerName} | {message}";

        _testOutputHelper.WriteLine(formattedMessage);
    }
}
