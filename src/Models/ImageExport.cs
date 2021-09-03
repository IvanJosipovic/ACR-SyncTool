namespace ACR_SyncTool.Models;

class ImageExport
{
    public string Image { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
}