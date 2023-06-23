using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScoopSearch.Indexer.Buckets.Providers;
using ScoopSearch.Indexer.Buckets.Sources;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.Extensions;
using ScoopSearch.Indexer.Git;
using ScoopSearch.Indexer.GitHub;
using ScoopSearch.Indexer.GitLab;
using ScoopSearch.Indexer.Indexer;
using ScoopSearch.Indexer.Manifest;
using ScoopSearch.Indexer.Processor;

namespace ScoopSearch.Indexer;

public static class ServicesExtensions
{
    public static void RegisterScoopSearchIndexer(this IServiceCollection @this)
    {
        // Options
        @this
            .AddOptions<BucketsOptions>()
            .Configure<IConfiguration>((options, configuration) => configuration.GetRequiredSection(BucketsOptions.Key).Bind(options));
        @this
            .AddOptions<AzureSearchOptions>()
            .Configure<IConfiguration>((options, configuration) => configuration.GetRequiredSection(AzureSearchOptions.Key).Bind(options));
        @this
            .AddOptions<GitHubOptions>()
            .Configure<IConfiguration>((options, configuration) => configuration.GetRequiredSection(GitHubOptions.Key).Bind(options));
        @this
            .AddOptions<GitLabOptions>()
            .Configure<IConfiguration>((options, configuration) => configuration.GetRequiredSection(GitLabOptions.Key).Bind(options));

        // Services
        @this.AddHttpClients();
        @this.AddSingleton<IGitRepositoryProvider, GitRepositoryProvider>();
        @this.AddSingleton<IGitHubClient, GitHubClient>();
        @this.AddSingleton<IGitLabClient, GitLabClient>();
        @this.AddSingleton<ISearchClient, AzureSearchClient>();
        @this.AddSingleton<ISearchIndex, AzureSearchIndex>();
        @this.AddSingleton<IKeyGenerator, KeyGenerator>();
        @this.AddSingleton<IIndexingProcessor, IndexingProcessor>();

        @this.AddSingleton<IBucketsProvider, GitHubBucketsProvider>();
        @this.AddSingleton<IBucketsProvider, GitLabBucketsProvider>();

        @this.AddSingleton<IOfficialBucketsSource, OfficialBucketsSource>();
        @this.AddSingleton<IBucketsSource, OfficialBucketsSource>();
        @this.AddSingleton<IBucketsSource, GitHubBucketsSource>();
        @this.AddSingleton<IBucketsSource, GitLabBucketsSource>();
        @this.AddSingleton<IBucketsSource, ManualBucketsListSource>();
        @this.AddSingleton<IBucketsSource, ManualBucketsSource>();

        @this.AddSingleton<IFetchManifestsProcessor, FetchManifestsProcessor>();
        @this.AddSingleton<IScoopSearchIndexer, ScoopSearchIndexer>();
    }
}
