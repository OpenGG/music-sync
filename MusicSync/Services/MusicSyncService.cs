using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Options;
using MusicSync.Models;
using MusicSync.Utils;

namespace MusicSync.Services;

public class MusicSyncService(
    DatabaseService db,
    HashService hashService,
    IOptions<Config> config,
    DrmPluginLoader pluginLoader,
    TemporaryDirectory rootTempDir
    )
{
    public async Task ProcessMusicLibrary()
    {
        foreach (var sourceDir in config.Value.MusicSources)
        {
            if (!Directory.Exists(sourceDir))
            {
                Console.WriteLine($"Warning: Source directory not found: {sourceDir}. Skipping.");
                continue;
            }

            Console.WriteLine($"\n--- Processing files from: {sourceDir} ---");

            var pipelineBuilder = new FileProcessingPipeline(config.Value, pluginLoader, rootTempDir, sourceDir);
            var (head, completion) = pipelineBuilder.CreatePipeline(db, hashService);

            foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                await head.SendAsync(file);
            }

            head.Complete();
            await completion;
        }

        Console.WriteLine("\n--- Music synchronization complete ---");
    }
}
