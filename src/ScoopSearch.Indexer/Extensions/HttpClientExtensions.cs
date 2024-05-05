using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using ScoopSearch.Indexer.Configuration;

namespace ScoopSearch.Indexer.Extensions;

internal static class HttpClientExtensions
{
    private const string DefaultHttpClient = "Default";
    private const string GitHubHttpClient = "GitHub";

    public static void AddHttpClients(this IServiceCollection services)
    {
        services
            .AddHttpClient(DefaultHttpClient)
            .AddTransientHttpErrorPolicy(policyBuilder => policyBuilder.WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        services
            .AddHttpClient(GitHubHttpClient, (serviceProvider, client) =>
        {
            // Github requires a user-agent
            var assemblyName = typeof(Extensions).Assembly.GetName();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
                assemblyName.Name!,
                assemblyName.Version!.ToString()));

            // Authentication to avoid API rate limitation
            var gitHubOptions = serviceProvider.GetRequiredService<IOptions<GitHubOptions>>();
            if (gitHubOptions.Value.Token == null)
            {
                serviceProvider.GetRequiredService<ILogger<HttpClient>>().LogWarning("GitHub Token is not defined in configuration.");
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", gitHubOptions.Value.Token);
        })
        .AddPolicyHandler((provider, _) => CreateGitHubRetryPolicy(provider));
    }

    public static HttpClient CreateDefaultClient(this IHttpClientFactory @this)
    {
        return @this.CreateClient(DefaultHttpClient);
    }

    public static HttpClient CreateGitHubClient(this IHttpClientFactory @this)
    {
        return @this.CreateClient(GitHubHttpClient);
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateGitHubRetryPolicy(IServiceProvider provider)
    {
        return Policy<HttpResponseMessage>
            .HandleResult(_ => _.StatusCode == HttpStatusCode.Forbidden)
            .OrTransientHttpError()
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
                    "GitHub HttpClient failed with code {StatusCode} and error {ErrorType} ({ErrorMessage}). Waiting {TimeSpan} before next retry. Retry attempt {RetryCount}.",
                    result.Result?.StatusCode,
                    result.Exception?.GetType(),
                    result.Exception?.Message,
                    delay,
                    retryAttempt);

                return delay;
            }, (_, _, _, _) => Task.CompletedTask);
    }
}

