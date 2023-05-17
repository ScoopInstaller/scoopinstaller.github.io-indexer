using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.Extensions;
using ScoopSearch.Indexer.Git;
using ScoopSearch.Indexer.GitHub;
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

        // Services
        @this.AddGitHubHttpClient(Constants.GitHubHttpClientName);
        @this.AddSingleton<IGitRepositoryProvider, GitRepositoryProvider>();
        @this.AddSingleton<IGitHubClient, GitHubClient>();
        @this.AddSingleton<ISearchClient, AzureSearchClient>();
        @this.AddSingleton<ISearchIndex, AzureSearchIndex>();
        @this.AddSingleton<IKeyGenerator, KeyGenerator>();
        @this.AddSingleton<IIndexingProcessor, IndexingProcessor>();
        @this.AddSingleton<IFetchBucketsProcessor, FetchBucketsProcessor>();
        @this.AddSingleton<IFetchManifestsProcessor, FetchManifestsProcessor>();
        @this.AddSingleton<IScoopSearchIndexer, ScoopSearchIndexer>();
    }
}
