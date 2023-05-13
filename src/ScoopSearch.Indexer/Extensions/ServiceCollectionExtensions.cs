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
            .AddPolicyHandler((provider, message) =>
            {
                var rateLimitPolicy = Policy.RateLimitAsync<HttpResponseMessage>(5, TimeSpan.FromSeconds(1));
                var retryPolicy = Policy<HttpResponseMessage>
                    .HandleResult(_ => _.StatusCode == HttpStatusCode.Forbidden)
                    .OrTransientHttpStatusCode()
                    .WaitAndRetryAsync(
                        6,
                        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                        (result, timeSpan, retryCount, _) =>
                        {
                            provider.GetRequiredService<ILogger<HttpClient>>().LogWarning(
                                "HttpClient {Name} failed with {StatusCode}. Waiting {TimeSpan} before next retry. Retry attempt {RetryCount}.",
                                name,
                                result.Result?.StatusCode,
                                timeSpan,
                                retryCount);
                        });

                return Policy.WrapAsync(rateLimitPolicy, retryPolicy);
            });
    }
}

