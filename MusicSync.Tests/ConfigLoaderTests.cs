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

    [Fact]
    public void Load_FileNotFound_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => ConfigLoader.Load("non_existent_file.json"));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public void Load_InvalidJson_ThrowsInvalidOperationException(string json)
    {
        using var jsonFile = new TemporaryFile("invalid.json").Create(json);
        Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(jsonFile.FilePath));
    }

    [Theory]
    [InlineData("{}", "Missing required property: 'music_sources'")]
    [InlineData("""{"music_sources": []}""", "Missing required property: 'music_dest_dir'")]
    public void Load_MissingRequiredProperty_ThrowsInvalidOperationException(string json, string expectedError)
    {
        using var jsonFile = new TemporaryFile("missing_prop.json").Create(json);
        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(jsonFile.FilePath));
        Assert.Equal($"Configuration file validation failed: {expectedError}", ex.Message);
    }

    [Theory]
    [InlineData("""{"music_sources": "not-an-array", "music_dest_dir": "", "drm_plugins": [], "music_extensions": []}""", "'music_sources' must be an array of strings.")]
    [InlineData("""{"music_sources": [], "music_dest_dir": 123, "drm_plugins": [], "music_extensions": []}""", "'music_dest_dir' must be a string.")]
    [InlineData("""{"music_sources": [], "music_dest_dir": "", "drm_plugins": "not-an-array", "music_extensions": []}""", "'drm_plugins' must be an array of objects.")]
    public void Load_InvalidPropertyType_ThrowsInvalidOperationException(string json, string expectedError)
    {
        using var jsonFile = new TemporaryFile("invalid_type.json").Create(json);
        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(jsonFile.FilePath));
        Assert.Equal($"Configuration file validation failed: {expectedError}", ex.Message);
    }

    [Theory]
    [InlineData("""{"music_sources": [], "music_dest_dir": "", "drm_plugins": [{}], "music_extensions": []}""", "Plugin in 'drm_plugins' must have a 'name' that is a string.")]
    [InlineData("""{"music_sources": [], "music_dest_dir": "", "drm_plugins": [{"name": "ncm"}], "music_extensions": []}""", "Plugin in 'drm_plugins' must have an 'enabled' property that is a boolean.")]
    [InlineData("""{"music_sources": [], "music_dest_dir": "", "drm_plugins": [{"name": "ncm", "enabled": true}], "music_extensions": []}""", "Plugin in 'drm_plugins' must have an 'extensions' array of strings.")]
    public void Load_InvalidDrmPluginConfig_ThrowsInvalidOperationException(string json, string expectedError)
    {
        using var jsonFile = new TemporaryFile("invalid_drm.json").Create(json);
        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(jsonFile.FilePath));
        Assert.Equal($"Configuration file validation failed: {expectedError}", ex.Message);
    }
}
