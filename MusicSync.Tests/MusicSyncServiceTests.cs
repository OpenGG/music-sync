using MusicSync.Services;

namespace MusicSync.Tests;

public class MusicSyncServiceTests
{
    [Fact]
    public void Run_ProcessesAll()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var inDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "x.mp3"), "data");
        var dbPath = Path.GetTempFileName();

        using var db = new DatabaseService(dbPath);
        var proc = new MusicFileProcessor(db, inDir, [".mp3"], new DrmPluginLoader([]));
        var service = new MusicSyncService(proc, [srcDir]);
        service.Run();

        Assert.True(File.Exists(Path.Combine(inDir, "x.mp3")));

        Directory.Delete(srcDir, true);
        Directory.Delete(inDir, true);
        File.Delete(dbPath);
    }
}
