using System.Diagnostics.CodeAnalysis;
using MusicSync.Services;
using MusicSync.Utils;

namespace MusicSync;

[ExcludeFromCodeCoverage]
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
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
            Console.Error.WriteLine("Error: The specified configuration file could not be found.");
            Console.Error.WriteLine($"File path: {e.FileName}");
            Console.Error.WriteLine($"Error details: {e.Message}");
            Console.Error.WriteLine("\n--- Stack Trace ---");
            Console.Error.WriteLine(e.StackTrace);
            return 1;
        }
        catch (Exception ex)
        {
            // This is a generic fallback for any other unhandled exception.
            Console.Error.WriteLine("Unhandled Exception: An unexpected error occurred during execution.");
            Console.Error.WriteLine($"Error details: {ex.Message}");
            Console.Error.WriteLine("\n--- Stack Trace ---");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
