using System.Collections.Generic;
using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Options;
using ScoopSearch.Functions.Configuration;
using ScoopSearch.Functions.Data;

namespace ScoopSearch.Functions.Indexer
{
    public class AzureSearchIndex
    {
        public const string StandardAnalyzer = "StandardAnalyzer";

        public const string ReverseAnalyzer = "ReverseAnalyzer";

        public const string PrefixAnalyzer = "PrefixAnalyzer";

        public const string SuffixAnalyzer = "SuffixAnalyzer";

        public const string UrlAnalyzer = "UrlAnalyzer";

        private const string ScoringProfile = "CustomProfile";

        private const string EdgeNGramTokenFilter = "EdgeNGramTokenFilter";

        private static readonly string[] CorsAllowedHosts = { "*" };

        private readonly SearchIndexClient _client;
        private readonly string _indexName;

        public AzureSearchIndex(IOptions<AzureSearchOptions> options)
        {
            _indexName = options.Value.IndexName;
            _client = new SearchIndexClient(options.Value.ServiceUrl, new AzureKeyCredential(options.Value.AdminApiKey));
        }

        public void CreateIndexIfRequired()
        {
            var index = new SearchIndex(_indexName);
            index.Fields = BuildFields();
            index.Analyzers.Add(BuildAnalyzer(StandardAnalyzer, LexicalTokenizerName.Standard, TokenFilterName.Lowercase));
            index.Analyzers.Add(BuildAnalyzer(PrefixAnalyzer, LexicalTokenizerName.Standard, TokenFilterName.Lowercase, EdgeNGramTokenFilter));
            index.Analyzers.Add(BuildAnalyzer(SuffixAnalyzer, LexicalTokenizerName.Standard, TokenFilterName.Lowercase, TokenFilterName.Reverse, EdgeNGramTokenFilter));
            index.Analyzers.Add(BuildAnalyzer(ReverseAnalyzer, LexicalTokenizerName.Standard, TokenFilterName.Lowercase, TokenFilterName.Reverse));
            index.Analyzers.Add(BuildAnalyzer(UrlAnalyzer, LexicalTokenizerName.UaxUrlEmail, TokenFilterName.Lowercase));
            index.TokenFilters.Add(BuildTokenFilter());
            index.ScoringProfiles.Add(BuildScoringProfile());
            index.DefaultScoringProfile = ScoringProfile;
            index.CorsOptions = new CorsOptions(CorsAllowedHosts);

            _client.CreateOrUpdateIndex(index);
        }

        private IList<SearchField> BuildFields()
        {
            return new FieldBuilder().Build(typeof(ManifestInfo));
        }

        private CustomAnalyzer BuildAnalyzer(string name, LexicalTokenizerName tokenizer, params TokenFilterName[] filters)
        {
            var analyzer = new CustomAnalyzer(name, tokenizer);
            filters.ForEach(_ => analyzer.TokenFilters.Add(_));

            return analyzer;
        }

        private TokenFilter BuildTokenFilter()
        {
            return new EdgeNGramTokenFilter(EdgeNGramTokenFilter)
            {
                MinGram = 2,
                MaxGram = 256,
                Side = EdgeNGramTokenFilterSide.Front
            };
        }

        private ScoringProfile BuildScoringProfile()
        {
            var textWeights = new TextWeights(
                new Dictionary<string, double>
                {
                    { ManifestInfo.NamePartialField, 40 },
                    { ManifestInfo.NameSuffixField, 40 },
                    { ManifestInfo.DescriptionField, 20 }
                }
            );

            var scoringFunctions = new List<ScoringFunction>
            {
                new MagnitudeScoringFunction(
                    ManifestMetadata.OfficialRepositoryNumberField,
                    10,
                    new MagnitudeScoringParameters(0, 1) { ShouldBoostBeyondRangeByConstant = false }),
                new MagnitudeScoringFunction(
                    ManifestMetadata.RepositoryStarsField,
                    10,
                    new MagnitudeScoringParameters(1, 100) { ShouldBoostBeyondRangeByConstant = true })
            };

            var scoringProfile = new ScoringProfile(ScoringProfile);
            scoringProfile.TextWeights = textWeights;
            scoringFunctions.ForEach(_ => scoringProfile.Functions.Add(_));

            return scoringProfile;
        }
    }
}
