using MusicSync.Models;
using MusicSync.Services;
using MusicSync.Utils;

namespace MusicSync.Tests;

public class MusicSyncServiceTests
{
    [Fact]
    public void Run_ProcessesAll()
    {
        using var srcDir = new TemporaryDirectory().Create();
        using var destDir = new TemporaryDirectory();
        using var tempDir = new TemporaryDirectory();

        using var srcFile = srcDir.CreateTemporaryFile("a.mp3", TestUtils.getMp3Bytes());

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

        Assert.True(File.Exists(Path.Join(destDir.DirectoryPath, "a.mp3")));
    }
}
