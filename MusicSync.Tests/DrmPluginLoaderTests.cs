using MusicSync.Models;
using MusicSync.Services;

namespace MusicSync.Tests;

public class DrmPluginLoaderTests
{
    [Fact]
    public void Load_FindsPlugin()
    {
        var pluginPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(pluginPath, "echo");
        System.Diagnostics.Process.Start("chmod", $"+x {pluginPath}").WaitForExit();
        var cfg = new DrmPluginConfig { Name = pluginPath, Enabled = true, Extensions = [".ncm"] };
        var loader = new DrmPluginLoader([cfg]);
        var plugin = loader.Resolve("file.ncm");
        Assert.NotNull(plugin);
        File.Delete(pluginPath);
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
