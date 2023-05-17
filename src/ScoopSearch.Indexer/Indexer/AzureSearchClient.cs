using System.Runtime.CompilerServices;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using MoreLinq;
using ScoopSearch.Indexer.Configuration;
using ScoopSearch.Indexer.Data;

namespace ScoopSearch.Indexer.Indexer;

internal class AzureSearchClient : ISearchClient
{
    private const int BatchSize = 1000;

    private readonly SearchClient _client;

    public AzureSearchClient(IOptions<AzureSearchOptions> options)
    {
        _client = new SearchClient(options.Value.ServiceUrl, options.Value.IndexName, new AzureKeyCredential(options.Value.AdminApiKey));
    }

    public async IAsyncEnumerable<ManifestInfo> GetExistingManifestsAsync(IEnumerable<Uri> repositories, [EnumeratorCancellation] CancellationToken token)
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

        var searchResults = await _client.SearchAsync<ManifestInfo>("*", options, token);
        await foreach(var searchResult in searchResults.Value.GetResultsAsync().WithCancellation(token))
        {
            yield return searchResult.Document;
        }
    }

    public async IAsyncEnumerable<ManifestInfo> GetAllManifestsAsync([EnumeratorCancellation] CancellationToken token)
    {
        var options = new SearchOptions();
        options.Select.Add(ManifestInfo.IdField);
        options.Select.Add(ManifestMetadata.RepositoryField);
        options.Select.Add(ManifestMetadata.RepositoryStarsField);
        options.Select.Add(ManifestMetadata.OfficialRepositoryNumberField);
        options.Select.Add(ManifestMetadata.ShaField);
        options.Select.Add(ManifestMetadata.CommittedField);
        options.Select.Add(ManifestMetadata.FilePathField);
        options.Select.Add(ManifestMetadata.DuplicateOfField);
        options.OrderBy.Add(ManifestInfo.IdField);
        options.IncludeTotalCount = true;
        
        // Batch retrieve manifests to overcome limitation of 100_000 documents per search
        string? lastId = null;
        bool hasResults;
        options.Size = 100_000;

        do
        {
            // Batch retrieve manifests using ranges
            if (lastId != null)
            {
                options.Filter = $"Id gt '{lastId}'";
            }

            var searchResults = await _client.SearchAsync<ManifestInfo>("*", options, token);
            hasResults = searchResults.Value.TotalCount > 0;
            await foreach (var searchResult in searchResults.Value.GetResultsAsync().WithCancellation(token))
            {
                lastId = searchResult.Document.Id;
                yield return searchResult.Document;
            }
        } while (hasResults);
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
        await Parallel.ForEachAsync(manifests.Batch(BatchSize), token, async (batch, _) => { await _client.DeleteDocumentsAsync(batch, null, _); });
    }

    public async Task UpsertManifestsAsync(IEnumerable<ManifestInfo> manifests, CancellationToken token)
    {
        await Parallel.ForEachAsync(manifests.Batch(BatchSize), token, async (batch, _) => { await _client.UploadDocumentsAsync(batch, null, _); });
    }
}
