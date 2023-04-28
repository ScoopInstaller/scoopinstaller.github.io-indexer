using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScoopSearch.Indexer;
using ScoopSearch.Indexer.Console;
using ScoopSearch.Indexer.Console.Interceptor;
using ScoopSearch.Indexer.Git;
using ScoopSearch.Indexer.GitHub;
using ScoopSearch.Indexer.Indexer;
using ScoopSearch.Indexer.Processor;

const string LogFile = "output.txt";

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.RegisterScoopSearchIndexer();

        // Decorate some classes with interceptors for logging purpose
        services.AddSingleton<IAsyncInterceptor, TimingInterceptor>();
        services.DecorateWithInterceptors<IGitRepositoryProvider, IAsyncInterceptor>();
        services.DecorateWithInterceptors<IGitHubClient, IAsyncInterceptor>();
        services.DecorateWithInterceptors<ISearchClient, IAsyncInterceptor>();
        services.DecorateWithInterceptors<ISearchIndex, IAsyncInterceptor>();
        services.DecorateWithInterceptors<IIndexingProcessor, IAsyncInterceptor>();
        services.DecorateWithInterceptors<IFetchBucketsProcessor, IAsyncInterceptor>();
        services.DecorateWithInterceptors<IFetchManifestsProcessor, IAsyncInterceptor>();
        services.DecorateWithInterceptors<IScoopSearchIndexer, IAsyncInterceptor>();
    })
    .ConfigureSerilog(LogFile)
    .Build();

await host.Services.GetRequiredService<IScoopSearchIndexer>().ExecuteAsync();
