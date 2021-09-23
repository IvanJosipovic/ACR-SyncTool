namespace ACR_SyncTool.Models;

public class ACRConfig
{
    public string Host { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
}