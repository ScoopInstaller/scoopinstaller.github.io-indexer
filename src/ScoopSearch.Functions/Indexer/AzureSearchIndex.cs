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
    public class AzureSearchIndex
    {
        public const string StandardAnalyzer = "StandardAnalyzer";

        public const string ReverseAnalyzer = "ReverseAnalyzer";

        public const string PrefixAnalyzer = "PrefixAnalyzer";

        public const string SuffixAnalyzer = "SuffixAnalyzer";

        public const string UrlAnalyzer = "UrlAnalyzer";

        private const string ScoringProfile = "CustomProfile";

        private const string EdgeNGramTokenFilter = "EdgeNGramTokenFilter";

        private readonly string[] CorsAllowedHosts = {"http://localhost:3000", "https://scoopsearch.github.io"};

        public void CreateIndexIfRequired(SearchServiceClient client, string indexName)
        {
            if (!client.Indexes.Exists(indexName))
            {
                var definition = new Index
                {
                    Name = indexName,
                    Fields = FieldBuilder.BuildForType<ManifestInfo>(),
                    Analyzers = new[]
                    {
                        new CustomAnalyzer
                        {
                            Name = StandardAnalyzer,
                            Tokenizer = TokenizerName.Standard,
                            TokenFilters = new[] { TokenFilterName.Lowercase }
                        },
                        new CustomAnalyzer
                        {
                            Name = PrefixAnalyzer,
                            Tokenizer = TokenizerName.Standard,
                            TokenFilters = new[] { TokenFilterName.Lowercase, EdgeNGramTokenFilter }
                        },
                        new CustomAnalyzer
                        {
                            Name = SuffixAnalyzer,
                            Tokenizer = TokenizerName.Standard,
                            TokenFilters = new[] { TokenFilterName.Lowercase, TokenFilterName.Reverse, EdgeNGramTokenFilter }
                        },
                        new CustomAnalyzer
                        {
                            Name = ReverseAnalyzer,
                            Tokenizer = TokenizerName.Standard,
                            TokenFilters = new[] { TokenFilterName.Lowercase, TokenFilterName.Reverse }
                        },
                        new CustomAnalyzer
                        {
                            Name = UrlAnalyzer,
                            Tokenizer = TokenizerName.UaxUrlEmail,
                            TokenFilters = new[] { TokenFilterName.Lowercase }
                        }
                    },
                    TokenFilters = new[]
                    {
                        new EdgeNGramTokenFilterV2
                        {
                            Name = EdgeNGramTokenFilter,
                            MinGram = 2,
                            MaxGram = 256,
                            Side = EdgeNGramTokenFilterSide.Front
                        },
                    },
                    ScoringProfiles = new[]
                    {
                        new ScoringProfile(
                            ScoringProfile,
                            new TextWeights(
                                new Dictionary<string, double>
                                {
                                    { ManifestInfo.NamePartialField, 40}, { ManifestInfo.NameSuffixField, 40}, {ManifestInfo.DescriptionField, 20}
                                }),
                            new List<ScoringFunction>
                            {
                                new MagnitudeScoringFunction(
                                    ManifestMetadata.OfficialRepositoryNumberField,
                                    2,
                                    0,
                                    1),
                                new MagnitudeScoringFunction(
                                    ManifestMetadata.RepositoryStarsField,
                                    2,
                                    1,
                                    500)
                            })
                    },
                    DefaultScoringProfile = ScoringProfile,
                    CorsOptions = new CorsOptions(CorsAllowedHosts, null)
                };

                client.Indexes.Create(definition);
            }
        }
    }
}
