using MusicSync.Services;
using MusicSync.Utils;

namespace MusicSync.Tests;

public class ConfigLoaderTests
{
    [Fact]
    public void Load_ValidFile_ReturnsConfig()
    {
        const string json = """
                            {
                                "music_sources": [
                                    "/path/to/music"
                                ],
                                "music_dest_dir": "/path/to/your/music_dest",
                                "database_file": "./music_sync.db",
                                "drm_plugins": [
                                    {
                                        "name": "ncmdump",
                                        "enabled": true,
                                        "extensions": [
                                            ".ncm"
                                        ]
                                    }
                                ],
                                "music_extensions": [
                                    ".mp3"
                                ]
                            }

                            """;
        using var jsonFile = new TemporaryFile(Path.GetRandomFileName())
            .Create(json);

        var cfg = ConfigLoader.Load(jsonFile.FilePath);
        Assert.Single(cfg.MusicSources);
    }
}
