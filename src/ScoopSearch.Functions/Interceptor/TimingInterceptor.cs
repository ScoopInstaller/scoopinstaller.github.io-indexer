using System.Diagnostics;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;

namespace ScoopSearch.Functions.Interceptor
{
    internal class TimingInterceptor : ProcessingAsyncInterceptor<Stopwatch>
    {
        private readonly ILogger _logger;

        public TimingInterceptor(ILogger<TimingInterceptor> logger)
        {
            _logger = logger;
        }

        protected override Stopwatch StartingInvocation(IInvocation invocation)
        {
            return Stopwatch.StartNew();
        }

        protected override void CompletedInvocation(IInvocation invocation, Stopwatch stopwatch)
        {
            _logger.LogDebug($"Executed '{invocation.Method.Name}({string.Join(", ", invocation.Arguments)})' in {stopwatch.Elapsed:g}");
        }
    }
}
