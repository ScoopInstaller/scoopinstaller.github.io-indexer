using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScoopSearch.Indexer;
using ScoopSearch.Indexer.Console;

const string LogFile = "output.txt";
TimeSpan Timeout = TimeSpan.FromMinutes(30);

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.RegisterScoopSearchIndexer();
    })
    .ConfigureSerilog(LogFile)
    .Build();

var cancellationToken = new CancellationTokenSource(Timeout).Token;
await host.Services.GetRequiredService<IScoopSearchIndexer>().ExecuteAsync(cancellationToken);
