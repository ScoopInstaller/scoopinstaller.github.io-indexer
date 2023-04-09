using System;

namespace ScoopSearch.Functions.Configuration
{
    public class AzureSearchOptions
    {
        public Uri ServiceUrl { get; set; } = null!;

        public string AdminApiKey { get; set; } = null!;

        public string IndexName { get; set; } = null!;
    }
}
