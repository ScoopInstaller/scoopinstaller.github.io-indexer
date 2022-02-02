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
        public GitHubRepo[] Items { get; private set; }
    }
}
