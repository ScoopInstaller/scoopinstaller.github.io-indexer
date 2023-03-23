using System;
using System.Linq;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Castle.DynamicProxy;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Extensions.Options;
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
            // Customize original IConfigurationRoot with additional providers
            CustomizeConfigurationRoot(builder.Services);

            // Options
            builder.Services
                .AddOptions<BucketsOptions>()
                .Configure<IConfiguration>((options, configuration) => configuration.GetSection(BucketsOptions.Key).Bind(options));
            builder.Services
                .AddOptions<AzureSearchOptions>()
                .Configure<IConfiguration>((options, configuration) =>
                {
                    options.ServiceName = configuration["AzureSearchServiceName"];
                    options.AdminApiKey = configuration["AzureSearchAdminApiKey"];
                    options.IndexName = configuration["AzureSearchIndexName"];
                });
            builder.Services
                .AddOptions<GitHubOptions>()
                .Configure<IConfiguration>((options, configuration) =>
                {
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
            builder.Services.AddSingleton<IGitRepository, GitRepository>();
            builder.Services.AddSingleton<IGitHubClient, GitHubClient>();
            builder.Services.AddSingleton<IManifestCrawler, ManifestCrawler>();
            builder.Services.AddSingleton<IIndexer, AzureSearchIndexer>();
            builder.Services.AddSingleton<AzureSearchIndex>();
            builder.Services.AddSingleton<IKeyGenerator, KeyGenerator>();

            // Decorate some classes with interceptors
            builder.Services.AddSingleton<IAsyncInterceptor, TimingInterceptor>();
            builder.Services.DecorateWithInterceptors<IGitRepository, IAsyncInterceptor>();
            builder.Services.DecorateWithInterceptors<IGitHubClient, IAsyncInterceptor>();
            builder.Services.DecorateWithInterceptors<IManifestCrawler, IAsyncInterceptor>();
            builder.Services.DecorateWithInterceptors<IIndexer, IAsyncInterceptor>();
        }

        private void CustomizeConfigurationRoot(IServiceCollection services)
        {
            // Remove original IConfiguration descriptor
            var originalConfigurationDescriptor = services.Single(descriptor =>
                descriptor.ServiceType == typeof(IConfiguration));
            services.Remove(originalConfigurationDescriptor);

            // Add new custom IConfiguration descriptor
            var updatedConfigurationDescriptor = new ServiceDescriptor(
                originalConfigurationDescriptor.ServiceType,
                serviceProvider => CreateCustomConfigurationRoot(serviceProvider, originalConfigurationDescriptor),
                originalConfigurationDescriptor.Lifetime);
            services.Add(updatedConfigurationDescriptor);
        }

        private IConfigurationRoot CreateCustomConfigurationRoot(IServiceProvider serviceProvider, ServiceDescriptor configurationDescriptor)
        {
            // Retrieve original providers
            var originalConfigurationRoot = (IConfigurationRoot?)(configurationDescriptor.ImplementationInstance ?? configurationDescriptor.ImplementationFactory?.Invoke(serviceProvider));
            if (originalConfigurationRoot == null)
            {
                throw new InvalidOperationException("Unable to retrieve original configuration root.");
            }

            var appDirectory = serviceProvider.GetRequiredService<IOptions<ExecutionContextOptions>>().Value.AppDirectory;

            // Create additional configuration providers
            var additionalConfigurationRoot = new ConfigurationBuilder()
                .SetBasePath(appDirectory)
                .AddJsonFile("settings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            // Merge all providers and create a new configuration root
            var providers = originalConfigurationRoot.Providers
                .Concat(additionalConfigurationRoot.Providers)
                .ToList();
            return new ConfigurationRoot(providers);
        }
    }
}
