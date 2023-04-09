using System;
using System.IO;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Castle.DynamicProxy;
using Microsoft.Azure.WebJobs.Host;
using ScoopSearch.Functions.Configuration;
using ScoopSearch.Functions.Git;
using ScoopSearch.Functions.GitHub;
using ScoopSearch.Functions.Indexer;
using ScoopSearch.Functions.Interceptor;
using ScoopSearch.Functions.Manifest;

[assembly: FunctionsStartup(typeof(ScoopSearch.Functions.Startup))]

namespace ScoopSearch.Functions
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            // Options
            builder.Services
                .AddOptions<BucketsOptions>()
                .Configure<IConfiguration>((options, configuration) => configuration.GetSection(BucketsOptions.Key).Bind(options));
            builder.Services
                .AddOptions<AzureSearchOptions>()
                .Configure<IConfiguration>((options, configuration) =>
                {
                    foreach (var key in new[] { "AzureSearchServiceUrl", "AzureSearchAdminApiKey", "AzureSearchIndexName" })
                    {
                        if (configuration[key] == null)
                        {
                            throw new InvalidOperationException($"'{key}' is not set");
                        }
                    }

                    options.ServiceUrl = new Uri(configuration["AzureSearchServiceUrl"]);
                    options.AdminApiKey = configuration["AzureSearchAdminApiKey"];
                    options.IndexName = configuration["AzureSearchIndexName"];
                });
            builder.Services
                .AddOptions<GitHubOptions>()
                .Configure<IConfiguration>((options, configuration) =>
                {
                    if (configuration["GitHubToken"] == null)
                    {
                        throw new InvalidOperationException("'GitHubToken' is not set");
                    }

                    options.Token = configuration["GitHubToken"];
                });
            builder.Services
                .AddOptions<QueuesOptions>()
                .Configure<IConfiguration>((options, configuration) =>
                {
                    // https://github.com/Azure/azure-functions-host/issues/5798
                    configuration.GetSection("AzureFunctionsJobHost:extensions:queues").Bind(options);
                });

            // Services
            builder.Services.AddHttpClient(Constants.GitHubHttpClientName, true);
            builder.Services.AddHttpClient(Constants.GitHubHttpClientNoRedirectName, false);
            builder.Services.AddSingleton<IGitRepositoryProvider, GitRepositoryProvider>();
            builder.Services.AddSingleton<IGitHubClient, GitHubClient>();
            builder.Services.AddSingleton<IManifestCrawler, ManifestCrawler>();
            builder.Services.AddSingleton<IIndexer, AzureSearchIndexer>();
            builder.Services.AddSingleton<AzureSearchIndex>();
            builder.Services.AddSingleton<IKeyGenerator, KeyGenerator>();

            // Decorate some classes with interceptors
            builder.Services.AddSingleton<IAsyncInterceptor, TimingInterceptor>();
            builder.Services.DecorateWithInterceptors<IGitRepositoryProvider, IAsyncInterceptor>();
            builder.Services.DecorateWithInterceptors<IGitHubClient, IAsyncInterceptor>();
            builder.Services.DecorateWithInterceptors<IManifestCrawler, IAsyncInterceptor>();
            builder.Services.DecorateWithInterceptors<IIndexer, IAsyncInterceptor>();
        }

        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            var context = builder.GetContext();
            builder.ConfigurationBuilder
                .AddJsonFile(Path.Combine(context.ApplicationRootPath, "settings.json"), optional: false, reloadOnChange: false);
        }
    }
}
