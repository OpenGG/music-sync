using MusicSync.Models;
using MusicSync.Services;
using MusicSync.Utils;

namespace MusicSync.Tests;

public class MusicFileProcessorTests
{
    [Fact]
    public void Process_RegularFile_Copies()
    {
        using var srcDir = new TemporaryDirectory().Create();
        using var destDir = new TemporaryDirectory();
        using var tempDir = new TemporaryDirectory();

        using var srcFile = srcDir.CreateTemporaryFile("a.mp3", TestUtils.GetMp3Bytes());

        using var dbFile = new TemporaryFile(Path.GetRandomFileName()).Create();
        using var db = new DatabaseService(dbFile.FilePath);

        var config = new Config
        {
            MusicSources = [srcDir.DirectoryPath],
            MusicDestDir = destDir.DirectoryPath,
            MusicExtensions = [".mp3"]
        };
        var proc = new MusicFileProcessor(db, config, tempDir, new DrmPluginLoader([]));
        proc.ProcessFile(srcFile.FilePath, srcDir.DirectoryPath);

        Assert.True(File.Exists(Path.Join(destDir.DirectoryPath, "a.mp3")));
    }

    [Fact]
    public void Process_DrmFile_UsesPlugin()
    {
        using var srcDir = new TemporaryDirectory().Create();
        using var destDir = new TemporaryDirectory();
        using var tempDir = new TemporaryDirectory();

        using var drmFile = srcDir.CreateTemporaryFile("b.ncm", TestUtils.GetMp3Bytes());

        using var pluginFile = new TemporaryFile(Path.GetRandomFileName()).Create(
            """
            #!/bin/sh
            cp $1 $2/out.mp3
            """);
        TestUtils.SetExecutable(pluginFile.FilePath);

        var cfg = new DrmPluginConfig { Name = pluginFile.FilePath, Enabled = true, Extensions = [".ncm"] };
        var loader = new DrmPluginLoader([cfg]);

        using var dbFile = new TemporaryFile(Path.GetRandomFileName()).Create();
        using var db = new DatabaseService(dbFile.FilePath);

        var config = new Config
        {
            MusicSources = [srcDir.DirectoryPath],
            MusicDestDir = destDir.DirectoryPath,
            MusicExtensions = [".mp3"]
        };

        var proc = new MusicFileProcessor(db, config, tempDir, loader);
        proc.ProcessFile(drmFile.FilePath, srcDir.DirectoryPath);

        Assert.True(File.Exists(Path.Join(destDir.DirectoryPath, "b.mp3")));
    }

    [Fact]
    public void ProcessFile_SkipsDuplicate()
    {
        using var srcDir = new TemporaryDirectory().Create();
        using var destDir = new TemporaryDirectory();
        using var tempDir = new TemporaryDirectory();

        using var srcFile = srcDir.CreateTemporaryFile("c.mp3", TestUtils.GetMp3Bytes());

        using var dbFile = new TemporaryFile(Path.GetRandomFileName()).Create();
        using var db = new DatabaseService(dbFile.FilePath);

        var config = new Config
        {
            MusicSources = [srcDir.DirectoryPath],
            MusicDestDir = destDir.DirectoryPath,
            MusicExtensions = [".mp3"]
        };
        var proc = new MusicFileProcessor(db, config, tempDir, new DrmPluginLoader([]));
        proc.ProcessFile(srcFile.FilePath, srcDir.DirectoryPath);
        proc.ProcessFile(srcFile.FilePath, srcDir.DirectoryPath);

        Assert.True(File.Exists(Path.Join(destDir.DirectoryPath, "c.mp3")));
        Assert.Single(Directory.GetFiles(destDir.DirectoryPath));
    }

    [Fact]
    public void ProcessFile_PluginError()
    {
        using var srcDir = new TemporaryDirectory().Create();
        using var destDir = new TemporaryDirectory();
        using var tempDir = new TemporaryDirectory();

        using var drmFile = srcDir.CreateTemporaryFile("fail.ncm", "data");

        using var dbFile = new TemporaryFile(Path.GetRandomFileName()).Create();
        using var db = new DatabaseService(dbFile.FilePath);

        using var pluginFile = new TemporaryFile(Path.GetRandomFileName()).Create("#!/bin/sh\nexit 1\n");
        TestUtils.SetExecutable(pluginFile.FilePath);

        var cfg = new DrmPluginConfig { Name = pluginFile.FilePath, Enabled = true, Extensions = [".ncm"] };
        var loader = new DrmPluginLoader([cfg]);

        var config = new Config
        {
            MusicSources = [srcDir.DirectoryPath],
            MusicDestDir = destDir.DirectoryPath,
            MusicExtensions = [".mp3"]
        };
        var proc = new MusicFileProcessor(db, config, tempDir, loader);
        proc.ProcessFile(drmFile.FilePath, srcDir.DirectoryPath);
        Assert.False(File.Exists(Path.Join(destDir.DirectoryPath, "fail.mp3")));
    }

    [Fact]
    public void ProcessFile_UnsupportedExtension()
    {
        using var srcDir = new TemporaryDirectory().Create();
        using var destDir = new TemporaryDirectory();
        using var tempDir = new TemporaryDirectory();

        using var dbFile = new TemporaryFile(Path.GetRandomFileName()).Create();
        using var db = new DatabaseService(dbFile.FilePath);

        using var unsupportedFile = srcDir.CreateTemporaryFile("unk.xyz", "data");

        var config = new Config
        {
            MusicSources = [srcDir.DirectoryPath],
            MusicDestDir = destDir.DirectoryPath,
            MusicExtensions = [".mp3"]
        };
        var proc = new MusicFileProcessor(db, config, tempDir, new DrmPluginLoader([]));
        proc.ProcessFile(unsupportedFile.FilePath, srcDir.DirectoryPath);
        Assert.False(Directory.Exists(destDir.DirectoryPath));
    }

    [Fact]
    public void ProcessFile_PluginNoOutput()
    {
        using var srcDir = new TemporaryDirectory().Create();
        using var destDir = new TemporaryDirectory();
        using var tempDir = new TemporaryDirectory();

        using var dbFile = new TemporaryFile(Path.GetRandomFileName()).Create();
        using var db = new DatabaseService(dbFile.FilePath);

        using var drmFile = srcDir.CreateTemporaryFile("empty.ncm", "data");

        using var pluginFile = new TemporaryFile(Path.GetRandomFileName()).Create("#!/bin/sh\nmkdir $2\n");
        TestUtils.SetExecutable(pluginFile.FilePath);

        var cfg = new DrmPluginConfig { Name = pluginFile.FilePath, Enabled = true, Extensions = [".ncm"] };
        var loader = new DrmPluginLoader([cfg]);

        var config = new Config
        {
            MusicSources = [srcDir.DirectoryPath],
            MusicDestDir = destDir.DirectoryPath,
            MusicExtensions = [".mp3"]
        };
        var proc = new MusicFileProcessor(db, config, tempDir, loader);
        proc.ProcessFile(drmFile.FilePath, srcDir.DirectoryPath);
        Assert.False(Directory.Exists(destDir.DirectoryPath));
    }
}
