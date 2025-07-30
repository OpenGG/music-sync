using MusicSync.Services;
using MusicSync.Utils;

namespace MusicSync.Tests;

public class DatabaseServiceTests
{
    [Fact]
    public void RecordAndCheckHash_Works()
    {
        using var dbFile = new TemporaryFile(Path.GetRandomFileName()).Create();
        using var db = new DatabaseService(dbFile.FilePath);

        Assert.False(db.IsMusicHashProcessed("abc"));
        db.RecordMusicHash("abc");
        Assert.True(db.IsMusicHashProcessed("abc"));
    }
}
