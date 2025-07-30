using MusicSync.Services;

namespace MusicSync.Tests;

public class ConfigLoaderTests
{
    [Fact]
    public void Load_ValidFile_ReturnsConfig()
    {
        const string yaml = "music_sources: ['a']";
        var path = Path.GetTempFileName();
        File.WriteAllText(path, yaml);
        var cfg = ConfigLoader.Load(path);
        Assert.Single(cfg.MusicSources);
        File.Delete(path);
    }
}
