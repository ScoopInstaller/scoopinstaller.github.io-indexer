using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using ScoopSearch.Indexer.Configuration;

namespace ScoopSearch.Indexer.Extensions;

internal static class ServiceCollectionExtensions
{
    public static IHttpClientBuilder AddHttpClient(this IServiceCollection services, string name, bool allowAutoRedirect)
    {
        return services
            .AddHttpClient(name, (serviceProvider, client) =>
            {
                // Github requires a user-agent
                var assemblyName = typeof(Extensions).Assembly.GetName();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
                    assemblyName.Name!,
                    assemblyName.Version!.ToString()));

                // Authentication to avoid API rate limitation
                var gitHubOptions = serviceProvider.GetRequiredService<IOptions<GitHubOptions>>();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", gitHubOptions.Value.Token);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler() { AllowAutoRedirect = allowAutoRedirect })
            .AddTransientHttpErrorPolicy(builder => builder.WaitAndRetryAsync(4, attempt => TimeSpan.FromSeconds(Math.Min(1, (attempt - 1) * 5))));
    }
}

