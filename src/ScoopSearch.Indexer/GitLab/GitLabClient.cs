using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ScoopSearch.Indexer.Extensions;

namespace ScoopSearch.Indexer.GitLab;

internal class GitLabClient : IGitLabClient
{
    private const string GitLabApiBaseUrl = "https://gitlab.com/api/v4";
    private const int ResultsPerPage = 100;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitLabClient> _logger;

    public GitLabClient(IHttpClientFactory httpClientFactory, ILogger<GitLabClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<GitLabRepo?> GetRepositoryAsync(Uri uri, CancellationToken cancellationToken)
    {
        var absolutePath = uri.AbsolutePath[1..];
        if (absolutePath.Count(c => c == '/') != 1)
        {
            _logger.LogWarning("{Uri} doesn't appear to be a valid GitLab project", uri);
            return null;
        }

        var apiUri = $"{GitLabApiBaseUrl}/projects/{WebUtility.UrlEncode(absolutePath)}";
        return await _httpClientFactory.CreateDefaultClient().GetStringAsync(apiUri, cancellationToken)
            .ContinueWith(task => task.Deserialize<GitLabRepo>(), cancellationToken);
    }

    public async IAsyncEnumerable<GitLabRepo> SearchRepositoriesAsync(string query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var gitLabTopics = await _httpClientFactory.CreateGitLabClient().GetStringAsync(GitLabApiBaseUrl + $"/topics?search={WebUtility.UrlEncode(query)}&per_page={ResultsPerPage}", cancellationToken)
            .ContinueWith(task => task.Deserialize<GitLabTopic[]>(), cancellationToken);

        if (gitLabTopics != null)
        {
            foreach (var gitLabTopic in gitLabTopics)
            {
                var gitLabRepos = await _httpClientFactory.CreateGitLabClient().GetStringAsync(GitLabApiBaseUrl + $"/projects?topic_id={gitLabTopic.Id}&per_page={ResultsPerPage}", cancellationToken)
                    .ContinueWith(task => task.Deserialize<GitLabRepo[]>(), cancellationToken);

                if (gitLabRepos != null)
                {
                    foreach (var gitLabRepo in gitLabRepos)
                    {
                        yield return gitLabRepo;
                    }
                }
            }
        }

    }

    private class GitLabTopic
    {
        [JsonInclude, JsonPropertyName("id")]
        public int Id { get; private set; }

        [JsonInclude, JsonPropertyName("total_projects_count")]
        public int Projects { get; private set; }
    }
}
