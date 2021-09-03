namespace ACR_SyncTool.Models;

public class RegistryConfig
{
    public string Host { get; set; } = string.Empty;
    public string? AuthType { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}