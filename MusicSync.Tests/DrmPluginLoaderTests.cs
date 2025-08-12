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

public class DrmPluginDecryptTests
{
    [Fact]
    public void Decrypt_Success()
    {
        using var pluginFile = new TemporaryFile("plugin.sh").Create(
            """
            #!/bin/sh
            touch "$2/output.mp3"
            exit 0
            """);
        TestUtils.SetExecutable(pluginFile.FilePath);

        var plugin = new MusicSync.Plugins.DrmPlugin("test-plugin", pluginFile.FilePath);
        using var tempDir = new TemporaryDirectory().Create();
        using var inputFile = new TemporaryFile("dummy.ncm").Create();

        var result = plugin.Decrypt(inputFile.FilePath, tempDir, [".mp3"]);

        Assert.NotNull(result);
        Assert.Equal(".mp3", Path.GetExtension(result));
        Assert.True(File.Exists(result));
    }

    [Fact]
    public void Decrypt_ScriptFailure()
    {
        using var pluginFile = new TemporaryFile("plugin.sh").Create(
            """
            #!/bin/sh
            echo "Failed" >&2
            exit 1
            """);
        TestUtils.SetExecutable(pluginFile.FilePath);

        var plugin = new MusicSync.Plugins.DrmPlugin("test-plugin", pluginFile.FilePath);
        using var tempDir = new TemporaryDirectory().Create();
        using var inputFile = new TemporaryFile("dummy.ncm").Create();

        var result = plugin.Decrypt(inputFile.FilePath, tempDir, [".mp3"]);

        Assert.Null(result);
    }

    [Fact]
    public void Decrypt_NoOutputFile()
    {
        using var pluginFile = new TemporaryFile("plugin.sh").Create(
            """
            #!/bin/sh
            # This script succeeds but creates no output file
            exit 0
            """);
        TestUtils.SetExecutable(pluginFile.FilePath);

        var plugin = new MusicSync.Plugins.DrmPlugin("test-plugin", pluginFile.FilePath);
        using var tempDir = new TemporaryDirectory().Create();
        using var inputFile = new TemporaryFile("dummy.ncm").Create();

        var result = plugin.Decrypt(inputFile.FilePath, tempDir, [".mp3"]);

        Assert.Null(result);
    }
}
