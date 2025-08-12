using Microsoft.Data.Sqlite;
using MusicSync.Models;
using MusicSync.Services;
using MusicSync.Utils;

namespace MusicSync.Tests;

public class MusicSyncServiceTests
{
    [Fact]
    public async Task Run_ProcessesAll()
    {
        using var srcDir = new TemporaryDirectory().Create();
        using var destDir = new TemporaryDirectory();
        using var tempDir = new TemporaryDirectory();

        using var srcFile = srcDir.CreateTemporaryFile("a.mp3", TestUtils.GetMp3Bytes());

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new DatabaseService(connection);
        await db.InitializeDatabaseAsync();

        var config = new Config
        {
            MusicSources = [srcDir.DirectoryPath],
            MusicDestDir = destDir.DirectoryPath,
            MusicExtensions = [".mp3"]
        };

        var hashService = new HashService();
        var pluginLoader = new DrmPluginLoader([]);
        var service = new MusicSyncService(db, hashService, config, pluginLoader, tempDir);
        await service.ProcessMusicLibrary();

        Assert.True(File.Exists(Path.Join(destDir.DirectoryPath, "a.mp3")));
    }
    [Fact]
    public async Task Run_MissingSourceDir_ShouldSkip()
    {
        using var destDir = new TemporaryDirectory();
        using var tempDir = new TemporaryDirectory();

        var missingPath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new DatabaseService(connection);
        await db.InitializeDatabaseAsync();

        var config = new Config
        {
            MusicSources = [missingPath],
            MusicDestDir = destDir.DirectoryPath,
            MusicExtensions = [".mp3"]
        };

        var hashService = new HashService();
        var pluginLoader = new DrmPluginLoader([]);
        var service = new MusicSyncService(db, hashService, config, pluginLoader, tempDir);

        var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);

        await service.ProcessMusicLibrary();

        Console.SetOut(originalOut);
        var output = sw.ToString();

        Assert.Contains($"Warning: Source directory not found: {missingPath}. Skipping.", output);
    }
}
