using MusicSync.Models;
using MusicSync.Services;

namespace MusicSync.Tests;

public class MusicFileProcessorTests
{
    private static string CreateTempFile(string dir, string name, string content)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Process_RegularFile_Copies()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var inDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var dbPath = Path.GetTempFileName();
        var file = CreateTempFile(srcDir, "a.mp3", "data");

        using var db = new DatabaseService(dbPath);
        var proc = new MusicFileProcessor(db, inDir, [".mp3"], new DrmPluginLoader([]));
        proc.ProcessFile(file, srcDir);

        Assert.True(File.Exists(Path.Combine(inDir, "a.mp3")));

        Directory.Delete(srcDir, true);
        Directory.Delete(inDir, true);
        File.Delete(dbPath);
    }

    [Fact]
    public void Process_DrmFile_UsesPlugin()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var inDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var dbPath = Path.GetTempFileName();
        var drmFile = CreateTempFile(srcDir, "b.ncm", "data");
        var pluginPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(pluginPath, "#!/bin/sh\ncp $1 $2/out.mp3\n");
        System.Diagnostics.Process.Start("chmod", $"+x {pluginPath}").WaitForExit();

        var cfg = new DrmPluginConfig { Name = pluginPath, Enabled = true, Extensions = [".ncm"] };
        var loader = new DrmPluginLoader([cfg]);

        using var db = new DatabaseService(dbPath);
        var proc = new MusicFileProcessor(db, inDir, [".mp3"], loader);
        proc.ProcessFile(drmFile, srcDir);

        Assert.True(File.Exists(Path.Combine(inDir, "b.mp3")));

        Directory.Delete(srcDir, true);
        Directory.Delete(inDir, true);
        File.Delete(dbPath);
        File.Delete(pluginPath);
    }

    [Fact]
    public void ProcessFile_SkipsDuplicate()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var inDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var dbPath = Path.GetTempFileName();
        var file = CreateTempFile(srcDir, "c.mp3", "data");

        using var db = new DatabaseService(dbPath);
        var proc = new MusicFileProcessor(db, inDir, [".mp3"], new DrmPluginLoader([]));
        proc.ProcessFile(file, srcDir);
        proc.ProcessFile(file, srcDir);

        Assert.True(File.Exists(Path.Combine(inDir, "c.mp3")));
        Assert.Single(Directory.GetFiles(inDir));

        Directory.Delete(srcDir, true);
        Directory.Delete(inDir, true);
        File.Delete(dbPath);
    }

    [Fact]
    public void ProcessFile_PluginError()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var inDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var dbPath = Path.GetTempFileName();
        var drmFile = CreateTempFile(srcDir, "fail.ncm", "data");
        var pluginPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(pluginPath, "#!/bin/sh\nexit 1\n");
        System.Diagnostics.Process.Start("chmod", $"+x {pluginPath}").WaitForExit();
        var cfg = new DrmPluginConfig { Name = pluginPath, Enabled = true, Extensions = [".ncm"] };
        var loader = new DrmPluginLoader([cfg]);
        using var db = new DatabaseService(dbPath);
        var proc = new MusicFileProcessor(db, inDir, [".mp3"], loader);
        proc.ProcessFile(drmFile, srcDir);
        Assert.False(File.Exists(Path.Combine(inDir, "fail.mp3")));
        if (Directory.Exists(srcDir)) Directory.Delete(srcDir, true);
        if (Directory.Exists(inDir)) Directory.Delete(inDir, true);
        if (File.Exists(dbPath)) File.Delete(dbPath);
        if (File.Exists(pluginPath)) File.Delete(pluginPath);
    }

    [Fact]
    public void ProcessFile_UnsupportedExtension()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var inDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var dbPath = Path.GetTempFileName();
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "unk.xyz"), "data");
        using var db = new DatabaseService(dbPath);
        var proc = new MusicFileProcessor(db, inDir, [".mp3"], new DrmPluginLoader([]));
        proc.ProcessFile(Path.Combine(srcDir, "unk.xyz"), srcDir);
        Assert.False(Directory.Exists(inDir));
        Directory.Delete(srcDir, true);
        File.Delete(dbPath);
    }

    [Fact]
    public void ProcessFile_PluginNoOutput()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var inDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var dbPath = Path.GetTempFileName();
        var drmFile = CreateTempFile(srcDir, "empty.ncm", "data");
        var pluginPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(pluginPath, "#!/bin/sh\nmkdir $2\n");
        System.Diagnostics.Process.Start("chmod", $"+x {pluginPath}").WaitForExit();
        var cfg = new DrmPluginConfig { Name = pluginPath, Enabled = true, Extensions = [".ncm"] };
        var loader = new DrmPluginLoader([cfg]);
        using var db = new DatabaseService(dbPath);
        var proc = new MusicFileProcessor(db, inDir, [".mp3"], loader);
        proc.ProcessFile(drmFile, srcDir);
        Assert.False(Directory.Exists(inDir));
        Directory.Delete(srcDir, true);
        File.Delete(dbPath);
        File.Delete(pluginPath);
    }
}
