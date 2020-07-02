using System.Linq;
using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;

namespace ScoopSearch.Functions.Interceptor
{
    public static class InterceptionExtensions
    {
        private static readonly ProxyGenerator _proxyGenerator = new ProxyGenerator();

        public static void DecorateWithInterceptors<TService, TInterceptor>(this IServiceCollection serviceCollection)
            where TService : class
            where TInterceptor : IAsyncInterceptor
        {
            serviceCollection.Decorate<TService>((instance, serviceProvider) =>
                _proxyGenerator.CreateInterfaceProxyWithTargetInterface(
                    instance,
                    serviceProvider
                        .GetServices<TInterceptor>()
                        .OfType<IAsyncInterceptor>()
                        .ToArray()));
        }
    }
}
