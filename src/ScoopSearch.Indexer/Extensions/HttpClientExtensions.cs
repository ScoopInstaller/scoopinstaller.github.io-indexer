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

    private static readonly TimeSpan HttpClientTimeout = TimeSpan.FromMinutes(5); 

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
            client.Timeout = HttpClientTimeout;
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

    private static Polly.Retry.AsyncRetryPolicy<HttpResponseMessage> CreateGitHubRetryPolicy(IServiceProvider provider)
    {
        return Policy<HttpResponseMessage>
            .HandleResult(_ => _.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            .OrTransientHttpError()
            .OrTransientHttpStatusCode()
            .WaitAndRetryAsync(5, (retryAttempt, response, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<HttpClient>>();
                logger.LogWarning(
                    "GitHub HttpClient failed to process request {Request} with result {Result} (Exception {ErrorType}).",
                    response.Result?.RequestMessage,
                    response.Result,
                    response.Exception);

                if (response.Exception is HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError })
                {
                    return TimeSpan.Zero;
                }

                TimeSpan delay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));

                // Secondary (abuse) rate limits are signalled with a Retry-After header. Primary rate
                // limits expose the reset time via X-RateLimit-Reset. Honor whichever the response
                // provides, and wait for the longest of the applicable hints.
                var retryAfter = response.Result?.Headers.RetryAfter;
                if (retryAfter?.Delta is { } retryAfterDelta)
                {
                    delay = Max(delay, retryAfterDelta);
                }
                else if (retryAfter?.Date is { } retryAfterDate)
                {
                    delay = Max(delay, retryAfterDate - DateTimeOffset.UtcNow);
                }

                if (response.Result?.Headers.TryGetValues("X-RateLimit-Reset", out var values) == true &&
                    response.Result.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) &&
                    Convert.ToInt32(remaining.Single()) == 0)
                {
                    var rateLimitReset = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(values.Single()));
                    delay = Max(delay, rateLimitReset - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1));
                }

                logger.LogWarning(
                    "Waiting {TimeSpan} before next retry. Retry attempt {RetryCount}.",
                    delay,
                    retryAttempt);

                return delay;
            }, (_, _, _, _) => Task.CompletedTask);
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}

