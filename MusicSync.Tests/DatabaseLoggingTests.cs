using System.IO;
using MusicSync.Services;
using Xunit;

namespace MusicSync.Tests
{
    public class DatabaseLoggingTests
    {
        [Fact]
        public void LogOperation_WritesAndReads()
        {
            var dbPath = Path.GetTempFileName();
            using var db = new DatabaseService(dbPath);
            db.LogOperation("file", 1, "md5", "copy_success");
            var result = db.FindPreviousResult("file", 1);
            Assert.Equal("copy_success", result);
            File.Delete(dbPath);
        }
    }
}
