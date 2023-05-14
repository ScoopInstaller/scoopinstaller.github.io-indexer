using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            .AddTransientHttpErrorPolicy(builder => builder.WaitAndRetryAsync(4, attempt => TimeSpan.FromSeconds(Math.Min(1, (attempt - 1) * 5))))
            .AddPolicyHandler((provider, _) =>
            {
                return Policy<HttpResponseMessage>
                    .HandleResult(result => result.IsSuccessStatusCode == false && result.StatusCode == HttpStatusCode.Forbidden)
                    .WaitAndRetryAsync(4, (_, result, _) =>
                    {
                        if (result.Result.Headers.TryGetValues("X-RateLimit-Reset", out var values))
                        {
                            var rateLimitReset = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(values.Single()));
                            var delay = rateLimitReset - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
                            provider.GetRequiredService<ILogger<HttpClient>>().LogWarning("Received GitHub rate-limit response. Waiting for {Delay} seconds before retrying", delay);

                            return delay;
                        }

                        throw new NotSupportedException("Unknown forbidden error");
                    }, (_, _, _, _) => Task.CompletedTask);
            });
    }
}

