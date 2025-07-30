using System.Collections.Generic;

namespace MusicSync.Models
{
    public class Config
    {
        public List<string> music_sources { get; set; } = new();
        public string music_incoming_dir { get; set; } = "music_incoming";
        public string database_file { get; set; } = "music_sync.db";
        public List<DrmPluginConfig> drm_plugins { get; set; } = new();
        public List<string> music_extensions { get; set; } = new();
    }

    public class DrmPluginConfig
    {
        public string name { get; set; } = string.Empty;
        public bool enabled { get; set; }
        public List<string> extensions { get; set; } = new();
    }
}
