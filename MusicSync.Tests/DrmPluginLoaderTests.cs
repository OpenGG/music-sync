using System.IO;
using MusicSync.Models;
using MusicSync.Services;
using Xunit;

namespace MusicSync.Tests
{
    public class DrmPluginLoaderTests
    {
        [Fact]
        public void Load_FindsPlugin()
        {
            var pluginPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllText(pluginPath, "echo");
            System.Diagnostics.Process.Start("chmod", $"+x {pluginPath}")!.WaitForExit();
            var cfg = new DrmPluginConfig { name = pluginPath, enabled = true, extensions = new() { ".ncm" } };
            var loader = new DrmPluginLoader(new[] { cfg });
            var plugin = loader.Resolve("file.ncm");
            Assert.NotNull(plugin);
            File.Delete(pluginPath);
        }

        [Fact]
        public void Load_IgnoresMissing()
        {
            var cfg = new DrmPluginConfig { name = "not_exists", enabled = true, extensions = new() { ".x" } };
            var loader = new DrmPluginLoader(new[] { cfg });
            var plugin = loader.Resolve("a.x");
            Assert.Null(plugin);
        }
    }
}
