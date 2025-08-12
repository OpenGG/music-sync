using Microsoft.Data.Sqlite;
using MusicSync.Models;
using MusicSync.Services;

namespace MusicSync.Tests;

public class DatabaseServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DatabaseService _dbService = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _dbService = new DatabaseService(_connection);
        await _dbService.InitializeDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task InitializeDatabaseAsync_ShouldCreateTablesAndAllIndexes()
    {
        var command = _connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='FileRecords';";
        Assert.Equal("FileRecords", (await command.ExecuteScalarAsync())!);

        command.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name='IDX_FileRecords_Path';";
        Assert.Equal("IDX_FileRecords_Path", (await command.ExecuteScalarAsync())!);

        command.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name='IDX_FileRecords_ContentHash';";
        Assert.Equal("IDX_FileRecords_ContentHash", (await command.ExecuteScalarAsync())!);

        command.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name='IDX_FileRecords_AudioFingerprint';";
        Assert.Equal("IDX_FileRecords_AudioFingerprint", (await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task CheckMetadataExistsAsync_ShouldReturnCorrectly()
    {
        var record = new FileContext { FilePath = "/test.mp3", RelativePath = "test.mp3", MTime = 12345 };
        await _dbService.BatchUpsertRecordsAsync(new[] { record });

        Assert.True(await _dbService.CheckMetadataExistsAsync("/test.mp3", 12345));
        Assert.False(await _dbService.CheckMetadataExistsAsync("/test.mp3", 54321));
        Assert.False(await _dbService.CheckMetadataExistsAsync("/other.mp3", 12345));
    }

    [Theory]
    [InlineData("ch1", "af1", true, true)]  // Both exist
    [InlineData("ch1", "af2", true, false)] // Content hash exists, fingerprint doesn't
    [InlineData("ch2", "af1", false, true)] // Fingerprint exists, content hash doesn't
    [InlineData("ch2", "af2", false, false)]// Neither exists
    public async Task CheckHashesAsync_ShouldReturnCorrectly_ForVariousCombinations(string contentHash, string audioFingerprint, bool expectedContent, bool expectedFingerprint)
    {
        var record = new FileContext { FilePath = "/test.mp3", RelativePath = "test.mp3", MTime = 1, ContentHash = "ch1", AudioFingerprint = "af1" };
        await _dbService.BatchUpsertRecordsAsync(new[] { record });

        var (contentExists, fingerprintExists) = await _dbService.CheckHashesAsync(contentHash, audioFingerprint);

        Assert.Equal(expectedContent, contentExists);
        Assert.Equal(expectedFingerprint, fingerprintExists);
    }

    [Fact]
    public async Task BatchUpsertRecordsAsync_ShouldInsertNewRecords()
    {
        var records = new List<FileContext>
        {
            new() { FilePath = "/path1.mp3", RelativePath = "path1.mp3", MTime = 1, ContentHash = "ch1", AudioFingerprint = "af1", Status = ProcessingStatus.Processed },
            new() { FilePath = "/path2.mp3", RelativePath = "path2.mp3", MTime = 2, ContentHash = "ch2", AudioFingerprint = "af2", Status = ProcessingStatus.SkippedContent }
        };

        await _dbService.BatchUpsertRecordsAsync(records);

        Assert.True(await _dbService.CheckMetadataExistsAsync("/path1.mp3", 1));
        var (ch1, af1) = await _dbService.CheckHashesAsync("ch1", "af1");
        Assert.True(ch1);
        Assert.True(af1);

        Assert.True(await _dbService.CheckMetadataExistsAsync("/path2.mp3", 2));
        var (ch2, af2) = await _dbService.CheckHashesAsync("ch2", "af2");
        Assert.True(ch2);
        Assert.True(af2);
    }

    [Fact]
    public async Task BatchUpsertRecordsAsync_ShouldUpdateExistingRecords()
    {
        var initialRecord = new FileContext { FilePath = "/test.mp3", RelativePath = "test.mp3", MTime = 1, ContentHash = "ch_old", AudioFingerprint = "af_old", Status = ProcessingStatus.Processed };
        await _dbService.BatchUpsertRecordsAsync(new[] { initialRecord });

        var updatedRecord = new FileContext { FilePath = "/test.mp3", RelativePath = "test.mp3", MTime = 2, ContentHash = "ch_new", AudioFingerprint = "af_new", Status = ProcessingStatus.SkippedFingerprint };
        await _dbService.BatchUpsertRecordsAsync(new[] { updatedRecord });

        // Old metadata should not exist
        Assert.False(await _dbService.CheckMetadataExistsAsync("/test.mp3", 1));
        // New metadata should exist
        Assert.True(await _dbService.CheckMetadataExistsAsync("/test.mp3", 2));

        // Old hashes should be gone
        var (ch_old, af_old) = await _dbService.CheckHashesAsync("ch_old", "af_old");
        Assert.False(ch_old);
        Assert.False(af_old);

        // New hashes should exist
        var (ch_new, af_new) = await _dbService.CheckHashesAsync("ch_new", "af_new");
        Assert.True(ch_new);
        Assert.True(af_new);
    }

    [Fact]
    public void FileRecord_Model_CanBeInstantiated()
    {
        // This test is purely to satisfy code coverage for the model's properties.
        var record = new FileRecord
        {
            Id = 1,
            AbsolutePath = "/path",
            MTime = 123,
            ContentHash = "ch",
            AudioFingerprint = "af",
            Status = ProcessingStatus.Processed,
            LastProcessedAt = System.DateTime.UtcNow
        };

        Assert.Equal(1, record.Id);
        Assert.Equal("/path", record.AbsolutePath);
        Assert.Equal(123, record.MTime);
        Assert.Equal("ch", record.ContentHash);
        Assert.Equal("af", record.AudioFingerprint);
        Assert.Equal(ProcessingStatus.Processed, record.Status);
        Assert.True(record.LastProcessedAt <= System.DateTime.UtcNow);
    }
}
