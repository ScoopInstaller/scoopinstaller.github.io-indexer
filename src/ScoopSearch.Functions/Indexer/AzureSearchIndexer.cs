using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Options;
using ScoopSearch.Functions.Configuration;
using ScoopSearch.Functions.Data;
using Index = Microsoft.Azure.Search.Models.Index;

namespace ScoopSearch.Functions.Indexer
{
    public class AzureSearchIndexer : IIndexer
    {
        private readonly ISearchIndexClient _index;

        public AzureSearchIndexer(AzureSearchIndex azureSearchIndex, IOptions<AzureSearchOptions> options)
        {
            var client = new SearchServiceClient(options.Value.ServiceName, new SearchCredentials(options.Value.AdminApiKey));
            azureSearchIndex.CreateIndexIfRequired(client, options.Value.IndexName!);
            _index = client.Indexes.GetClient(options.Value.IndexName);
        }

        public async Task<IEnumerable<ManifestInfo>> GetExistingManifestsAsync(Uri repository, CancellationToken token)
        {
            var parameters = new SearchParameters()
            {
                Select = new[]
                {
                    ManifestInfo.IdField,
                    ManifestMetadata.RepositoryField,
                    ManifestMetadata.RepositoryStarsField,
                    ManifestMetadata.ShaField,
                },
                Filter = $"{ManifestMetadata.RepositoryField} eq '{repository.AbsoluteUri}'",
                OrderBy = new[] { ManifestInfo.IdField },
                Top = int.MaxValue // Retrieve pages of 1000 items
            };

            var searchResults = new List<SearchResult<ManifestInfo>>();
            var search = await _index.Documents.SearchAsync<ManifestInfo>("*", parameters, null, token);
            searchResults.AddRange(search.Results);
            while (search.ContinuationToken != null)
            {
                search = await _index.Documents.ContinueSearchAsync<ManifestInfo>(search.ContinuationToken, null, token);
                searchResults.AddRange(search.Results);
            }

            return searchResults.Select(x => x.Document);
        }

        public async Task<IEnumerable<Uri>> GetBucketsAsync(CancellationToken token)
        {
            var parameters = new SearchParameters
            {
                Facets = new []{ $"{ManifestMetadata.RepositoryField},count:{int.MaxValue}"},
                Top = 0 // Facets only
            };

            var facetResults = new List<FacetResult>();
            var search = await _index.Documents.SearchAsync(string.Empty, parameters, null, token);
            facetResults.AddRange(search.Facets.SelectMany(x => x.Value));
            while (search.ContinuationToken != null)
            {
                search = await _index.Documents.ContinueSearchAsync(search.ContinuationToken, null, token);
                facetResults.AddRange(search.Facets.SelectMany(x => x.Value));
            }

            return facetResults.Select(x => new Uri(x.AsValueFacetResult<string>().Value));
        }

        public async Task DeleteManifestsAsync(IEnumerable<ManifestInfo> manifests, CancellationToken token)
        {
            var batch = IndexBatch.Delete(manifests);
            await _index.Documents.IndexAsync(batch, null, token);
        }

        public async Task AddManifestsAsync(IEnumerable<ManifestInfo> manifests, CancellationToken token)
        {
            var batch = IndexBatch.MergeOrUpload(manifests);
            await _index.Documents.IndexAsync(batch, null, token);
        }
    }
}
