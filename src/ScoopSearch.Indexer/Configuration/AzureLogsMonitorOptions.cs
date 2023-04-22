namespace ScoopSearch.Indexer.Configuration;

public class AzureLogsMonitorOptions
{
    public string TenantId { get; set; } = null!;

    public string ClientId { get; set; } = null!;

    public string ClientSecret { get; set; } = null!;

    public string WorkspaceId { get; set; } = null!;
}
