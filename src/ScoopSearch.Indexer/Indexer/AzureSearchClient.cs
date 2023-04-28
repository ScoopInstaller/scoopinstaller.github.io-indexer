using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.Data;

namespace ScoopSearch.Indexer.Indexer;

internal class AzureSearchClient : ISearchClient
{
    private readonly SearchClient _client;

    public AzureSearchClient(IOptions<AzureSearchOptions> options)
    {
        _client = new SearchClient(options.Value.ServiceUrl, options.Value.IndexName, new AzureKeyCredential(options.Value.AdminApiKey));
    }

    public async Task<IEnumerable<ManifestInfo>> GetExistingManifestsAsync(IEnumerable<Uri> repositories, CancellationToken token)
    {
        var options = new SearchOptions();
        options.Select.Add(ManifestInfo.IdField);
        options.Select.Add(ManifestMetadata.RepositoryField);
        options.Select.Add(ManifestMetadata.RepositoryStarsField);
        options.Select.Add(ManifestMetadata.OfficialRepositoryNumberField);
        options.Select.Add(ManifestMetadata.ShaField);
        options.Filter = $"search.in({ManifestMetadata.RepositoryField}, '{string.Join(",", repositories.Select(_ => _.AbsoluteUri))}')";
        options.OrderBy.Add(ManifestInfo.IdField);
        options.Size = int.MaxValue; // Retrieve as many results as possible

        var results = new List<ManifestInfo>();
        var searchResults = await _client.SearchAsync<ManifestInfo>("*", options, token);
        await foreach(var searchResult in searchResults.Value.GetResultsAsync().WithCancellation(token))
        {
            results.Add(searchResult.Document);
        }

        return results;
    }

    public async Task<IEnumerable<ManifestInfo>> GetAllManifestsAsync(CancellationToken token)
    {
        var options = new SearchOptions();
        options.Select.Add(ManifestInfo.IdField);
        options.Select.Add(ManifestMetadata.RepositoryField);
        options.Select.Add(ManifestMetadata.RepositoryStarsField);
        options.Select.Add(ManifestMetadata.OfficialRepositoryNumberField);
        options.Select.Add(ManifestMetadata.ShaField);
        options.Select.Add(ManifestMetadata.CommittedField);
        options.Select.Add(ManifestMetadata.FilePathField);
        options.OrderBy.Add(ManifestInfo.IdField);
        options.Size = int.MaxValue; // Retrieve as many results as possible

        var results = new List<ManifestInfo>();
        var searchResults = await _client.SearchAsync<ManifestInfo>("*", options, token);
        await foreach(var searchResult in searchResults.Value.GetResultsAsync().WithCancellation(token))
        {
            results.Add(searchResult.Document);
        }

        return results;
    }

    public async Task<IEnumerable<Uri>> GetBucketsAsync(CancellationToken token)
    {
        var options = new SearchOptions();
        options.Facets.Add($"{ManifestMetadata.RepositoryField},count:{int.MaxValue}");
        options.Size = 0; // Facets only

        var search = await _client.SearchAsync<SearchDocument>(string.Empty, options, token);
        return search.Value.Facets[ManifestMetadata.RepositoryField]
            .Select(_ => new Uri(_.AsValueFacetResult<string>().Value));
    }

    public async Task DeleteManifestsAsync(IEnumerable<ManifestInfo> manifests, CancellationToken token)
    {
        await _client.DeleteDocumentsAsync(manifests, null, token);
    }

    public async Task UpsertManifestsAsync(IEnumerable<ManifestInfo> manifests, CancellationToken token)
    {
        await _client.UploadDocumentsAsync(manifests, null, token);
    }
}
