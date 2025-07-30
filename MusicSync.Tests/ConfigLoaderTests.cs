using System.IO;
using MusicSync.Services;
using MusicSync.Models;
using Xunit;

namespace MusicSync.Tests
{
    public class ConfigLoaderTests
    {
        [Fact]
        public void Load_ValidFile_ReturnsConfig()
        {
            var yaml = "music_sources: ['a']";
            var path = Path.GetTempFileName();
            File.WriteAllText(path, yaml);
            var cfg = ConfigLoader.Load(path);
            Assert.Single(cfg.music_sources);
            File.Delete(path);
        }
    }
}
