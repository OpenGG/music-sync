using System.Diagnostics.CodeAnalysis;
using MusicSync.Services;
using MusicSync.Utils;

namespace MusicSync;

[ExcludeFromCodeCoverage]
public static class Program
{
    public static int Main(string[] args)
    {
        try {
            FfmpegUtil.CheckFfmpeg();
    
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
        catch (FileNotFoundException e)
        {
            // Handle the case where the config file is missing
            Console.Error.WriteLine($"Error: file not found\n  {e}");

            Console.Error.WriteLine($"Error details: {e.Message}");

            // Optionally, log the full stack trace for debugging
            Console.Error.WriteLine(e.StackTrace);
            return 1;
        }
        catch (Exception ex)
        {
            // Catch any other deserialization errors
            Console.Error.WriteLine("Unhandled Exception: Exception during execution");
            Console.Error.WriteLine($"Error details: {ex.Message}");

            // Optionally, log the full stack trace for debugging
            Console.Error.WriteLine(ex.StackTrace);

            return 1;
        }
    }
}
