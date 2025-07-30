using MusicSync.Models;
using MusicSync.Services;
using MusicSync.Utils;

namespace MusicSync.Tests;

public class MusicSyncServiceMissingDirTests
{
    [Fact]
    public void Run_IgnoresMissingDir()
    {
        using var srcDir = new TemporaryDirectory().Create();
        using var destDir = new TemporaryDirectory();
        using var tempDir = new TemporaryDirectory();

        using var dbFile = new TemporaryFile(Path.GetRandomFileName()).Create();
        using var db = new DatabaseService(dbFile.FilePath);

        var config = new Config
        {
            MusicSources = [srcDir.DirectoryPath],
            MusicDestDir = destDir.DirectoryPath,
            MusicExtensions = [".mp3"]
        };
        var proc = new MusicFileProcessor(db, config, tempDir, new DrmPluginLoader([]));
        var service = new MusicSyncService(proc, [srcDir.DirectoryPath]);
        service.Run();

        Assert.False(Directory.Exists(destDir.DirectoryPath));
        // no exception means success
    }
}
