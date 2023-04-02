using Microsoft.Extensions.Logging;
using Moq;

namespace ScoopSearch.Functions.Tests.Helpers;

public static class LoggerMockExtensions
{
    public static void VerifyLog<TCategoryName>(this Mock<ILogger<TCategoryName>> @this, LogLevel logLevel, string message)
    {
        @this.VerifyLog(logLevel, _ => _ == message);
    }

    public static void VerifyLog<TCategoryName>(this Mock<ILogger<TCategoryName>> @this, LogLevel logLevel, Predicate<string> message)
    {
        @this.VerifyLog(logLevel, message, (Exception? _) => _ == null);
    }

    public static void VerifyLog<TCategoryName, TException>(this Mock<ILogger<TCategoryName>> @this, LogLevel logLevel, string message)
        where TException : Exception
    {
        @this.VerifyLog<TCategoryName, TException>(logLevel, message, _ => _.GetType() == typeof(TException));
    }

    public static void VerifyLog<TCategoryName, TException>(this Mock<ILogger<TCategoryName>> @this, LogLevel logLevel, Predicate<string> messagePredicate)
        where TException : Exception
    {
        @this.VerifyLog<TCategoryName, TException>(logLevel, messagePredicate, _ => _.GetType() == typeof(TException));
    }

    public static void VerifyLog<TCategoryName, TException>(this Mock<ILogger<TCategoryName>> @this, LogLevel logLevel, string message, Predicate<TException> exceptionPredicate)
        where TException : Exception
    {
        @this.VerifyLog(logLevel, _ => _ == message, exceptionPredicate);
    }

    public static void VerifyLog<TCategoryName, TException>(this Mock<ILogger<TCategoryName>> @this, LogLevel logLevel, Predicate<string> message, Predicate<TException> exception)
        where TException : Exception?
    {
        @this.Verify(logger => logger.Log(
            logLevel,
            0,
            It.Is<It.IsAnyType>((@object, _) => message(@object.ToString()!)),
            It.Is<TException>(_ => exception(_)),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
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
