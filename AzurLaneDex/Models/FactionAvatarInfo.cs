namespace AzurLaneDex.Models;

public class FactionAvatarInfo
{
    public string FactionId { get; set; } = "";
    public string LocalImagePath { get; set; } = "";
    public string Version { get; set; } = "";
    public bool IsDownloaded { get; set; }
}