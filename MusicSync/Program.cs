using MusicSync.Services;

using System.Diagnostics.CodeAnalysis;

namespace MusicSync;

[ExcludeFromCodeCoverage]
public static class Program
{
    public static int Main(string[] args)
    {
        string? configPath = null;
        if (args.Length >= 2 && (args[0] == "-c" || args[0] == "--config"))
            configPath = args[1];

        var config = ConfigLoader.Load(configPath);
        var pluginLoader = new DrmPluginLoader(config.DrmPlugins);

        using var db = new DatabaseService(string.IsNullOrEmpty(config.DatabaseFile) ? "music_sync.db" : config.DatabaseFile);
        var processor = new MusicFileProcessor(db, config.MusicIncomingDir, config.MusicExtensions.Select(e => e.ToLower()).ToArray(), pluginLoader);
        var service = new MusicSyncService(processor, config.MusicSources);
        service.Run();
        return 0;
    }
}
