using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MusicSync.Models;
using MusicSync.Services;
using MusicSync.Utils;

namespace MusicSync;

[ExcludeFromCodeCoverage]
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            await FfmpegUtil.CheckFfmpegAsync();

            string? configPath = null;
            if (args.Length >= 2 && (args[0] == "-c" || args[0] == "--config"))
                configPath = args[1];

            var config = ConfigLoader.Load(configPath);

            var services = new ServiceCollection();
            services.AddSingleton<IOptions<Config>>(Options.Create(config));
            services.AddSingleton<HashService>();
            services.AddSingleton<DrmPluginLoader>();
            services.AddScoped<TemporaryDirectory>();
            services.AddScoped<DatabaseService>();
            services.AddScoped<MusicSyncService>();

            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();

            var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();
            await db.InitializeDatabaseAsync();
            var service = scope.ServiceProvider.GetRequiredService<MusicSyncService>();
            await service.ProcessMusicLibrary();

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
