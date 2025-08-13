using Microsoft.Extensions.Options;
using MusicSync.Models;
using MusicSync.Services;
using MusicSync.Utils;

namespace MusicSync.Tests;

public class MusicSyncServiceMissingDirTests
{
    [Fact]
    public async Task Run_IgnoresMissingDir()
    {
        using var srcDir = new TemporaryDirectory();
        var missingPath = srcDir.DirectoryPath;
        using var destDir = new TemporaryDirectory();
        using var tempDir = new TemporaryDirectory();

        using var dbFile = new TemporaryFile(Path.GetRandomFileName()).Create();
        await using var db = new DatabaseService(dbFile.FilePath);
        await db.InitializeDatabaseAsync();

        var config = new Config
        {
            MusicSources = [missingPath],
            MusicDestDir = destDir.DirectoryPath,
            MusicExtensions = [".mp3"]
        };

        var hashService = new HashService();
        var options = Options.Create(config);
        var pluginLoader = new DrmPluginLoader(options);
        var service = new MusicSyncService(db, hashService, options, pluginLoader, tempDir);
        await service.ProcessMusicLibrary();

        Assert.False(Directory.Exists(destDir.DirectoryPath));
        // no exception means success
    }
}
