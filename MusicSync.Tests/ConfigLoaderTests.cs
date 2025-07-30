using MusicSync.Services;
using MusicSync.Utils;

namespace MusicSync.Tests;

public class ConfigLoaderTests
{
    [Fact]
    public void Load_ValidFile_ReturnsConfig()
    {
        const string yaml = "music_sources: ['a']";
        using var yamlFile = new TemporaryFile(Path.GetRandomFileName())
            .Create(yaml);

        var cfg = ConfigLoader.Load(yamlFile.FilePath);
        Assert.Single(cfg.MusicSources);
    }
}
