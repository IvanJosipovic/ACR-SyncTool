namespace ACR_SyncTool.Models;

public class SyncedImage
{
    public string Image { get; set; } = string.Empty;
    public string Semver { get; set; } = string.Empty;
    public string Regex { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
}