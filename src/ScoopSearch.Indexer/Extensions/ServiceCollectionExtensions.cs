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
    public static IHttpClientBuilder AddHttpClient(this IServiceCollection services, string name, bool followAutoRedirect)
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
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler() { AllowAutoRedirect = followAutoRedirect })
            .AddPolicyHandler((provider, _) => CreateRetryPolicy(provider, name));
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(IServiceProvider provider, string httpClientName)
    {
        return Policy<HttpResponseMessage>
            .HandleResult(_ => _.StatusCode == HttpStatusCode.Forbidden)
            .OrTransientHttpStatusCode()
            .WaitAndRetryAsync(5, (retryAttempt, result, _) =>
            {
                TimeSpan delay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                if (result.Result?.StatusCode == HttpStatusCode.Forbidden && result.Result.Headers.TryGetValues("X-RateLimit-Reset", out var values))
                {
                    var rateLimitReset = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(values.Single()));
                    delay = rateLimitReset - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
                }

                provider.GetRequiredService<ILogger<HttpClient>>().LogWarning(
                    "HttpClient {Name} failed with {StatusCode}. Waiting {TimeSpan} before next retry. Retry attempt {RetryCount}.",
                    httpClientName,
                    result.Result?.StatusCode,
                    delay,
                    retryAttempt);

                return delay;
            }, (_, _, _, _) => Task.CompletedTask);
    }
}

