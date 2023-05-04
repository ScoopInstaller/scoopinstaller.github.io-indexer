using System.Diagnostics;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;

namespace ScoopSearch.Indexer.Console.Interceptor;

internal class TimingInterceptor : AsyncTimingInterceptor
{
    private readonly ILogger _logger;

    public TimingInterceptor(ILogger<TimingInterceptor> logger)
    {
        _logger = logger;
    }

    protected override void StartingTiming(IInvocation invocation)
    {
    }

    protected override void CompletedTiming(IInvocation invocation, Stopwatch stopwatch)
    {
        _logger.LogDebug("Executed '{Method}({Arguments})' in {Duration:g}", invocation.Method.Name, string.Join(", ", invocation.Arguments), stopwatch.Elapsed);
    }
}
