using MusicSync.Services;
using MusicSync.Utils;

namespace MusicSync.Tests;

public class DatabaseLoggingTests
{
    [Fact]
    public void LogOperation_WritesAndReads()
    {
        using var dbFile = new TemporaryFile(Path.GetRandomFileName()).Create();
        using var db = new DatabaseService(dbFile.FilePath);

        db.LogOperation("file", 1, "mock_hash", "copy_success");
        var result = db.FindPreviousResult("file", 1);
        Assert.Equal("copy_success", result);
    }
}
