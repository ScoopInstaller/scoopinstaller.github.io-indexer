using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ScoopSearch.Indexer.Configuration;
using Serilog;
using Serilog.Enrichers.Sensitive;
using Serilog.Events;
using Serilog.Filters;
using Serilog.Formatting.Compact;

namespace ScoopSearch.Indexer.Console;

public static class LoggingExtensions
{
    public static IHostBuilder ConfigureSerilog(this IHostBuilder @this, string logFile)
    {
        return @this.UseSerilog((context, provider, configure) =>
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }

            configure
                .MinimumLevel.Debug()
                .Enrich.WithSensitiveDataMasking(options =>
                {
                    options.MaskingOperators.Clear();
                    options.MaskingOperators.Add(new TokensMaskingOperator(
                        provider.GetRequiredService<IOptions<GitHubOptions>>().Value.Token,
                        provider.GetRequiredService<IOptions<AzureSearchOptions>>().Value.AdminApiKey
                    ));
                })
                .WriteTo.File(new CompactJsonFormatter(), logFile)
                .WriteTo.Logger(options => options
                    .MinimumLevel.Information()
                    // Exclude non important HttpClient logs from the console
                    .Filter.ByExcluding(_ => Matching.FromSource(typeof(HttpClient).FullName)(_) && _.Level < LogEventLevel.Warning)
                    .WriteTo.Console());
        });
    }

    private class TokensMaskingOperator : RegexMaskingOperator
    {
        public TokensMaskingOperator(params string[] tokens)
            : base(string.Join("|", tokens.Select(_ => $"({Regex.Escape(_)})")))
        {
        }
    }
}
