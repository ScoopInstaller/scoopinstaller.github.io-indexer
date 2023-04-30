using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace ScoopSearch.Indexer.Tests.Helpers;

public class XUnitLogger : ILogger
{
    private readonly ILogger _logger;

    public XUnitLogger(string categoryName, ITestOutputHelper testOutputHelper)
    {
        _logger = new XUnitLoggerMock<object>(testOutputHelper, categoryName).Object;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _logger.Log(logLevel, eventId, state, exception, formatter);

    public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => _logger.BeginScope(state)!;
}

public class XUnitLogger<TCategoryName> : ILogger<TCategoryName>
{
    public XUnitLogger(ITestOutputHelper testOutputHelper)
    {
        Mock = new XUnitLoggerMock<TCategoryName>(testOutputHelper);
    }

    public Mock<ILogger<TCategoryName>> Mock { get; }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Mock.Object.Log(logLevel, eventId, state, exception, formatter);

    public bool IsEnabled(LogLevel logLevel) => Mock.Object.IsEnabled(logLevel);

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => Mock.Object.BeginScope(state)!;
}
