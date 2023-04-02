using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ScoopSearch.Functions.Tests.Helpers;

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

        public AndConstraint<LoggerMockAssertions<TLogger>> Log(LogLevel logLevel, string message)
        {
            _mock.VerifyLog(logLevel, message);
            return new AndConstraint<LoggerMockAssertions<TLogger>>(this);
        }

        public AndConstraint<LoggerMockAssertions<TLogger>> Log(LogLevel logLevel, Predicate<string> message)
        {
            _mock.VerifyLog(logLevel, message);
            return new AndConstraint<LoggerMockAssertions<TLogger>>(this);
        }

        public AndConstraint<LoggerMockAssertions<TLogger>> Log<TException>(LogLevel logLevel, string message)
            where TException : Exception
        {
            _mock.VerifyLog<TLogger, TException>(logLevel, message);
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
