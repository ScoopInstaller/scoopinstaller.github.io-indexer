using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.Data;

namespace ScoopSearch.Indexer.Indexer;

public class AzureSearchIndexer : IIndexer
{
    private readonly SearchClient _client;

    public AzureSearchIndexer(AzureSearchIndex azureSearchIndex, IOptions<AzureSearchOptions> options)
    {
        azureSearchIndex.CreateIndexIfRequired();
        _client = new SearchClient(options.Value.ServiceUrl, options.Value.IndexName, new AzureKeyCredential(options.Value.AdminApiKey));
    }

    public async Task<IEnumerable<ManifestInfo>> GetExistingManifestsAsync(Uri repository, CancellationToken token)
    {
        var options = new SearchOptions();
        options.Select.Add(ManifestInfo.IdField);
        options.Select.Add(ManifestMetadata.RepositoryField);
        options.Select.Add(ManifestMetadata.RepositoryStarsField);
        options.Select.Add(ManifestMetadata.OfficialRepositoryNumberField);
        options.Select.Add(ManifestMetadata.ShaField);
        options.Filter = $"{ManifestMetadata.RepositoryField} eq '{repository.AbsoluteUri}'";
        options.OrderBy.Add(nameof(ManifestInfo.Id));
        options.Size = int.MaxValue; // Retrieve as many results as possible

        var results = new List<ManifestInfo>();
        var searchResults = await _client.SearchAsync<ManifestInfo>("*", options, token);
        if (searchResults.HasValue)
        {
            await foreach(var searchResult in searchResults.Value.GetResultsAsync().WithCancellation(token))
            {
                results.Add(searchResult.Document);
            }
        }

        return results;
    }

    public async Task<IEnumerable<Uri>> GetBucketsAsync(CancellationToken token)
    {
        var options = new SearchOptions();
        options.Facets.Add($"{ManifestMetadata.RepositoryField},count:{int.MaxValue}");
        options.Size = 0; // Facets only

        var search = await _client.SearchAsync<SearchDocument>(string.Empty, options, token);
        if (search.HasValue)
        {
            return search.Value.Facets[ManifestMetadata.RepositoryField]
                .Select(x => new Uri(x.AsValueFacetResult<string>().Value));
        }

        return Enumerable.Empty<Uri>();
    }

    public async Task DeleteManifestsAsync(IEnumerable<ManifestInfo> manifests, CancellationToken token)
    {
        await _client.DeleteDocumentsAsync(manifests, null, token);
    }

    public async Task AddManifestsAsync(IEnumerable<ManifestInfo> manifests, CancellationToken token)
    {
        await _client.UploadDocumentsAsync(manifests, null, token);
    }
}
