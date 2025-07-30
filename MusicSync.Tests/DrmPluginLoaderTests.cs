using MusicSync.Models;
using MusicSync.Services;
using MusicSync.Utils;

namespace MusicSync.Tests;

public class DrmPluginLoaderTests
{
    [Fact]
    public void Load_FindsPlugin()
    {
        using var pluginFile = new TemporaryFile(Path.GetRandomFileName()).Create(
            """
            #!/bin/sh
            echo
            """);
        TestUtils.SetExecutable(pluginFile.FilePath);

        var cfg = new DrmPluginConfig { Name = pluginFile.FilePath, Enabled = true, Extensions = [".ncm"] };
        var loader = new DrmPluginLoader([cfg]);
        var plugin = loader.Resolve("file.ncm");
        Assert.NotNull(plugin);
    }

    [Fact]
    public void Load_IgnoresMissing()
    {
        var cfg = new DrmPluginConfig { Name = "not_exists", Enabled = true, Extensions = [".x"] };
        var loader = new DrmPluginLoader([cfg]);
        var plugin = loader.Resolve("a.x");
        Assert.Null(plugin);
    }
}
