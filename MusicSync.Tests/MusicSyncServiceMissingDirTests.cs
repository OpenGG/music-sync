using MusicSync.Services;

namespace MusicSync.Tests;

public class MusicSyncServiceMissingDirTests
{
    [Fact]
    public void Run_IgnoresMissingDir()
    {
        var missing = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var db = new DatabaseService(Path.GetTempFileName());
        var proc = new MusicFileProcessor(db, Path.GetTempPath(), [".mp3"], new DrmPluginLoader([]));
        var service = new MusicSyncService(proc, [missing]);
        service.Run();
        // no exception means success
    }
}
