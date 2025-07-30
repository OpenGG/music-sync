using System.Diagnostics.CodeAnalysis;
using MusicSync.Services;
using MusicSync.Utils;

namespace MusicSync;

[ExcludeFromCodeCoverage]
public static class Program
{
    public static int Main(string[] args)
    {
        string? configPath = null;
        if (args.Length >= 2 && (args[0] == "-c" || args[0] == "--config"))
            configPath = args[1];

        using var rootTempDir = new TemporaryDirectory();

        var config = ConfigLoader.Load(configPath);
        var pluginLoader = new DrmPluginLoader(config.DrmPlugins);

        using var db = new DatabaseService(config.DatabaseFile);
        var processor = new MusicFileProcessor(db, config, rootTempDir, pluginLoader);
        var service = new MusicSyncService(processor, config.MusicSources);
        service.Run();
        return 0;
    }
}
