using Microsoft.Extensions.Logging;
using Moq;

namespace ScoopSearch.Functions.Tests.Helpers;

public static class LoggerMockExtensions
{
    public static void VerifyLog<TCategoryName>(this Mock<ILogger<TCategoryName>> @this, LogLevel logLevel, string message, Times? times = null)
    {
        @this.VerifyLog(logLevel, _ => _ == message, times);
    }

    public static void VerifyLog<TCategoryName>(this Mock<ILogger<TCategoryName>> @this, LogLevel logLevel, Predicate<string> message, Times? times = null)
    {
        @this.VerifyLog(logLevel, message, (Exception? _) => _ == null, times);
    }

    public static void VerifyLog<TCategoryName, TException>(this Mock<ILogger<TCategoryName>> @this, LogLevel logLevel, string message, Times? times = null)
        where TException : Exception
    {
        @this.VerifyLog<TCategoryName, TException>(logLevel, message, _ => _.GetType() == typeof(TException), times);
    }

    public static void VerifyLog<TCategoryName, TException>(this Mock<ILogger<TCategoryName>> @this, LogLevel logLevel, Predicate<string> messagePredicate, Times? times = null)
        where TException : Exception
    {
        @this.VerifyLog<TCategoryName, TException>(logLevel, messagePredicate, _ => _.GetType() == typeof(TException), times);
    }

    private static void VerifyLog<TCategoryName, TException>(this Mock<ILogger<TCategoryName>> @this, LogLevel logLevel, string message, Predicate<TException> exceptionPredicate, Times? times)
        where TException : Exception
    {
        @this.VerifyLog(logLevel, _ => _ == message, exceptionPredicate, times);
    }

    private static void VerifyLog<TCategoryName, TException>(this Mock<ILogger<TCategoryName>> @this, LogLevel logLevel, Predicate<string> message, Predicate<TException> exception, Times? times)
        where TException : Exception?
    {
        @this.Verify(logger => logger.Log(
            logLevel,
            0,
            It.Is<It.IsAnyType>((@object, _) => message(@object.ToString()!)),
            It.Is<TException>(_ => exception(_)),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times.GetValueOrDefault(Times.Once()));
    }

    public static void VerifyNoLog<TCategoryName>(this Mock<ILogger<TCategoryName>> @this, LogLevel logLevelOrHigher)
    {
        @this.Verify(logger => logger.Log(
            It.Is<LogLevel>(logLevel => logLevel >= logLevelOrHigher),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }
}
