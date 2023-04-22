using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ScoopSearch.Indexer.Tests.Helpers;

public static class LoggerMockAssertionsExtensions
{

    public static LoggerMockAssertions<TCategoryName> Should<TCategoryName>(this XUnitLogger<TCategoryName> @this)
    {
        return new LoggerMockAssertions<TCategoryName>(@this);
    }

    public class LoggerMockAssertions<TLogger>
    {
        private readonly Mock<ILogger<TLogger>> _mock;

        public LoggerMockAssertions(XUnitLogger<TLogger> logger)
        {
            _mock = logger.Mock;
        }

        public AndConstraint<LoggerMockAssertions<TLogger>> Log(LogLevel logLevel, string message, Times? times = null)
        {
            _mock.VerifyLog(logLevel, message, times);
            return new AndConstraint<LoggerMockAssertions<TLogger>>(this);
        }

        public AndConstraint<LoggerMockAssertions<TLogger>> Log(LogLevel logLevel, Predicate<string> message, Times? times = null)
        {
            _mock.VerifyLog(logLevel, message, times);
            return new AndConstraint<LoggerMockAssertions<TLogger>>(this);
        }

        public AndConstraint<LoggerMockAssertions<TLogger>> Log<TException>(LogLevel logLevel, string message, Times? times = null)
            where TException : Exception
        {
            _mock.VerifyLog<TLogger, TException>(logLevel, message, times);
            return new AndConstraint<LoggerMockAssertions<TLogger>>(this);
        }

        public AndConstraint<LoggerMockAssertions<TLogger>> Log<TException>(LogLevel logLevel, Predicate<string> message)
            where TException : Exception
        {
            _mock.VerifyLog<TLogger, TException>(logLevel, message);
            return new AndConstraint<LoggerMockAssertions<TLogger>>(this);
        }

        public void NoLog(LogLevel logLevelOrHigher)
        {
            _mock.VerifyNoLog(logLevelOrHigher);
        }
    }
}
