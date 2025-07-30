using MusicSync.Models;
using MusicSync.Services;

using System.Diagnostics.CodeAnalysis;

namespace MusicSync
{
    [ExcludeFromCodeCoverage]
    public class Program
    {
        public static int Main(string[] args)
        {
            string? configPath = null;
            if (args.Length >= 2 && (args[0] == "-c" || args[0] == "--config"))
                configPath = args[1];

            Config config = ConfigLoader.Load(configPath);
            var pluginLoader = new DrmPluginLoader(config.drm_plugins);

            using var db = new DatabaseService(string.IsNullOrEmpty(config.database_file) ? "music_sync.db" : config.database_file);
            var processor = new MusicFileProcessor(db, config.music_incoming_dir, config.music_extensions.Select(e => e.ToLower()).ToArray(), pluginLoader);
            var service = new MusicSyncService(processor, config.music_sources);
            service.Run();
            return 0;
        }
    }
}
