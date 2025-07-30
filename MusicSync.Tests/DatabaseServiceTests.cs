using System.IO;
using MusicSync.Services;
using Xunit;

namespace MusicSync.Tests
{
    public class DatabaseServiceTests
    {
        [Fact]
        public void RecordAndCheckHash_Works()
        {
            var dbPath = Path.GetTempFileName();
            using var db = new DatabaseService(dbPath);
            Assert.False(db.IsMusicHashProcessed("abc"));
            db.RecordMusicHash("abc");
            Assert.True(db.IsMusicHashProcessed("abc"));
            File.Delete(dbPath);
        }
    }
}
