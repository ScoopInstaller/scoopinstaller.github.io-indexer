using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
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
            .AddPolicyHandler((provider, _) => HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == HttpStatusCode.Forbidden)
                .WaitAndRetryAsync(
                    new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30) },
                    (result, timeSpan, retryCount, context) =>
                    {
                        provider.GetRequiredService<ILogger<HttpClient>>().LogWarning("GitHub API request failed with {StatusCode}. Waiting {TimeSpan} before next retry. Retry attempt {RetryCount}.", result.Result?.StatusCode, timeSpan, retryCount);
                    }));
    }
}

