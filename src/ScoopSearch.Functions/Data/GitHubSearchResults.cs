using System;
using Newtonsoft.Json;

namespace ScoopSearch.Functions.Data
{
    public class GitHubSearchResults
    {
        [JsonConstructor]
        private GitHubSearchResults()
        {
        }

        [JsonProperty("total_count")]
        public int TotalCount { get; private set; }

        [JsonProperty("items")]
        public GitHubSearchResult[] Items { get; private set; }

        public class GitHubSearchResult
        {
            [JsonConstructor]
            private GitHubSearchResult()
            {
            }

            [JsonProperty("html_url")]
            public Uri HtmlUri { get; private set; }

            [JsonProperty("stargazers_count")]
            public int Stars { get; private set; }
        }
    }
}
