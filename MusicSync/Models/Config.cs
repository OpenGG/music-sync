namespace MusicSync.Models;

public class Config
{
    public List<string> MusicSources { get; set; } = [];
    public string MusicDestDir { get; set; } = "music_dest";
    public string DatabaseFile { get; set; } = "music_sync.db";
    public List<DrmPluginConfig> DrmPlugins { get; set; } = [];
    public List<string> MusicExtensions { get; set; } = [];
}

public class DrmPluginConfig
{
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public List<string> Extensions { get; init; } = [];
}
