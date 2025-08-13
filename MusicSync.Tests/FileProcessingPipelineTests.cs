using System.Threading.Tasks.Dataflow;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Moq;
using MusicSync.Models;
using MusicSync.Plugins;
using MusicSync.Services;
using MusicSync.Utils;

namespace MusicSync.Tests;

public class FileProcessingPipelineTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DatabaseService _dbService = null!;
    private Mock<HashService> _mockHashService = null!;
    private Mock<DrmPluginLoader> _mockDrmPluginLoader = null!;
    private Config _config = null!;
    private TemporaryDirectory _tempDir = null!;
    private string SourceDir => Path.Combine(_tempDir.DirectoryPath, "source");

    public async Task InitializeAsync()
    {
        _tempDir = new TemporaryDirectory().Create();
        _connection = new SqliteConnection($"Data Source={Path.Combine(_tempDir.DirectoryPath, "test.db")}");
        await _connection.OpenAsync();
        _dbService = new DatabaseService(_connection);
        await _dbService.InitializeDatabaseAsync();

        _mockHashService = new Mock<HashService>();
        _mockDrmPluginLoader = new Mock<DrmPluginLoader>(Options.Create(new Config())) { CallBase = true };
        _config = new Config
        {
            MusicExtensions = new List<string> { ".mp3" },
            MusicDestDir = Path.Combine(_tempDir.DirectoryPath, "dest")
        };

        Directory.CreateDirectory(SourceDir);
        Directory.CreateDirectory(_config.MusicDestDir);
    }

    public async Task DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
        _tempDir.Dispose();
        if (Directory.Exists(SourceDir))
        {
            Directory.Delete(SourceDir, true);
        }
        if (Directory.Exists(_config.MusicDestDir))
        {
            Directory.Delete(_config.MusicDestDir, true);
        }
    }

    [Fact]
    public async Task Scenario1_MetadataHit_SkipsProcessing()
    {
        using var tempFile = new TemporaryFile("test.mp3", SourceDir).Create();
        var mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(tempFile.FilePath)).ToUnixTimeSeconds();
        await _dbService.BatchUpsertRecordsAsync(new[] { new FileContext { FilePath = tempFile.FilePath, RelativePath = Path.GetRelativePath(SourceDir, tempFile.FilePath), MTime = mtime } });

        var pipeline = new FileProcessingPipeline(_config, _mockDrmPluginLoader.Object, _tempDir, SourceDir);
        var (head, completion) = pipeline.CreatePipeline(_dbService, _mockHashService.Object);

        await head.SendAsync(tempFile.FilePath);
        head.Complete();
        await completion;

        _mockHashService.Verify(h => h.ComputeContentHashAsync(It.IsAny<string>()), Times.Never);
        _mockHashService.Verify(h => h.ComputeAudioFingerprintAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Scenario2_ContentHashHit_SkipsFingerprintAndCopy()
    {
        using var tempFile = new TemporaryFile("test.mp3", SourceDir).Create();
        _mockHashService.Setup(h => h.ComputeContentHashAsync(tempFile.FilePath)).ReturnsAsync("content_hash");
        _mockHashService.Setup(h => h.ComputeAudioFingerprintAsync(tempFile.FilePath)).ReturnsAsync("fingerprint");

        var dbMock = new Mock<DatabaseService>(_connection) { CallBase = true };
        dbMock.Setup(db => db.CheckHashesAsync("content_hash", "fingerprint")).ReturnsAsync((true, false));

        var pipeline = new FileProcessingPipeline(_config, _mockDrmPluginLoader.Object, _tempDir, SourceDir);
        var (head, completion) = pipeline.CreatePipeline(dbMock.Object, _mockHashService.Object);

        await head.SendAsync(tempFile.FilePath);
        head.Complete();
        await completion;

        dbMock.Verify(db => db.BatchUpsertRecordsAsync(It.Is<IEnumerable<FileContext>>(l => l.First().Status == ProcessingStatus.SkippedContent)), Times.Once);
    }

    [Fact]
    public async Task Scenario3_FingerprintHit_SkipsCopy()
    {
        using var tempFile = new TemporaryFile("test.mp3", SourceDir).Create();
        _mockHashService.Setup(h => h.ComputeContentHashAsync(tempFile.FilePath)).ReturnsAsync("content_hash");
        _mockHashService.Setup(h => h.ComputeAudioFingerprintAsync(tempFile.FilePath)).ReturnsAsync("fingerprint");

        var dbMock = new Mock<DatabaseService>(_connection) { CallBase = true };
        dbMock.Setup(db => db.CheckHashesAsync("content_hash", "fingerprint")).ReturnsAsync((false, true));

        var pipeline = new FileProcessingPipeline(_config, _mockDrmPluginLoader.Object, _tempDir, SourceDir);
        var (head, completion) = pipeline.CreatePipeline(dbMock.Object, _mockHashService.Object);

        await head.SendAsync(tempFile.FilePath);
        head.Complete();
        await completion;

        dbMock.Verify(db => db.BatchUpsertRecordsAsync(It.Is<IEnumerable<FileContext>>(l => l.First().Status == ProcessingStatus.SkippedFingerprint)), Times.Once);
    }

    [Fact]
    public async Task Scenario4_NoCacheHit_ProcessesFully()
    {
        using var tempFile = new TemporaryFile("test.mp3", SourceDir).Create();
        _mockHashService.Setup(h => h.ComputeContentHashAsync(tempFile.FilePath)).ReturnsAsync("content_hash");
        _mockHashService.Setup(h => h.ComputeAudioFingerprintAsync(tempFile.FilePath)).ReturnsAsync("fingerprint");

        var dbMock = new Mock<DatabaseService>(_connection) { CallBase = true };
        dbMock.Setup(db => db.CheckHashesAsync("content_hash", "fingerprint")).ReturnsAsync((false, false));

        var pipeline = new FileProcessingPipeline(_config, _mockDrmPluginLoader.Object, _tempDir, SourceDir);
        var (head, completion) = pipeline.CreatePipeline(dbMock.Object, _mockHashService.Object);

        await head.SendAsync(tempFile.FilePath);
        head.Complete();
        await completion;

        dbMock.Verify(db => db.BatchUpsertRecordsAsync(It.Is<IEnumerable<FileContext>>(l => l.First().Status == ProcessingStatus.Processed)), Times.Once);
        Assert.Single(Directory.EnumerateFiles(_config.MusicDestDir));
    }

    [Fact]
    public async Task HashFailure_IsHandled()
    {
        using var tempFile = new TemporaryFile("test.mp3", SourceDir).Create();
        _mockHashService.Setup(h => h.ComputeContentHashAsync(tempFile.FilePath)).ThrowsAsync(new System.Exception("Hash failed"));

        var dbMock = new Mock<DatabaseService>(_connection) { CallBase = true };

        var pipeline = new FileProcessingPipeline(_config, _mockDrmPluginLoader.Object, _tempDir, SourceDir);
        var (head, completion) = pipeline.CreatePipeline(dbMock.Object, _mockHashService.Object);

        await head.SendAsync(tempFile.FilePath);
        head.Complete();
        await completion;

        dbMock.Verify(db => db.BatchUpsertRecordsAsync(It.Is<IEnumerable<FileContext>>(contexts =>
            contexts.Any(c => c.Status == ProcessingStatus.HashFailure)
        )), Times.Once);
    }

    [Fact]
    public async Task UnsupportedFile_IsHandled()
    {
        using var tempFile = new TemporaryFile("test.txt", SourceDir).Create(); // Unsupported extension
        var dbMock = new Mock<DatabaseService>(_connection) { CallBase = true };

        var pipeline = new FileProcessingPipeline(_config, _mockDrmPluginLoader.Object, _tempDir, SourceDir);
        var (head, completion) = pipeline.CreatePipeline(dbMock.Object, _mockHashService.Object);

        await head.SendAsync(tempFile.FilePath);
        head.Complete();
        await completion;

        dbMock.Verify(db => db.BatchUpsertRecordsAsync(It.Is<IEnumerable<FileContext>>(contexts =>
            contexts.Any(c => c.Status == ProcessingStatus.Unsupported)
        )), Times.Once);
    }

    [Fact]
    public async Task DrmDecryptionFailure_IsHandled()
    {
        using var tempFile = new TemporaryFile("test.ncm", SourceDir).Create();
        var mockPlugin = new Mock<DrmPlugin>("ncm", "ncm.sh");

        _mockDrmPluginLoader.Setup(loader => loader.Resolve(tempFile.FilePath)).Returns(mockPlugin.Object);
        mockPlugin.Setup(p => p.Decrypt(It.IsAny<string>(), It.IsAny<TemporaryDirectory>(), It.IsAny<string[]>()))
                  .Returns((string?)null); // Simulate decryption failure

        var dbMock = new Mock<DatabaseService>(_connection) { CallBase = true };
        var pipeline = new FileProcessingPipeline(_config, _mockDrmPluginLoader.Object, _tempDir, SourceDir);
        var (head, completion) = pipeline.CreatePipeline(dbMock.Object, _mockHashService.Object);

        await head.SendAsync(tempFile.FilePath);
        head.Complete();
        await completion;

        dbMock.Verify(db => db.BatchUpsertRecordsAsync(It.Is<IEnumerable<FileContext>>(contexts =>
            contexts.Any(c => c.Status == ProcessingStatus.DrmFailure)
        )), Times.Once);
    }

    [Fact]
    public async Task DrmDecryptionSuccess_ProcessesDecryptedFile()
    {
        // Arrange
        using var tempDrmFile = new TemporaryFile("test.ncm", SourceDir).Create();
        using var tempDecryptedFile = new TemporaryFile("decrypted.mp3").Create();

        var mockPlugin = new Mock<DrmPlugin>("ncm", "ncm.sh");
        _mockDrmPluginLoader.Setup(loader => loader.Resolve(tempDrmFile.FilePath)).Returns(mockPlugin.Object);

        // When Decrypt is called, it returns the path to a real, temporary decrypted file.
        mockPlugin.Setup(p => p.Decrypt(tempDrmFile.FilePath, It.IsAny<TemporaryDirectory>(), It.IsAny<string[]>()))
            .Returns(tempDecryptedFile.FilePath);

        _mockHashService.Setup(h => h.ComputeContentHashAsync(tempDecryptedFile.FilePath)).ReturnsAsync("decrypted_content_hash");
        _mockHashService.Setup(h => h.ComputeAudioFingerprintAsync(tempDecryptedFile.FilePath)).ReturnsAsync("decrypted_fingerprint");

        var dbMock = new Mock<DatabaseService>(_connection) { CallBase = true };
        dbMock.Setup(db => db.CheckHashesAsync("decrypted_content_hash", "decrypted_fingerprint")).ReturnsAsync((false, false));

        var pipeline = new FileProcessingPipeline(_config, _mockDrmPluginLoader.Object, _tempDir, SourceDir);
        var (head, completion) = pipeline.CreatePipeline(dbMock.Object, _mockHashService.Object);

        // Act
        await head.SendAsync(tempDrmFile.FilePath);
        head.Complete();
        await completion;

        // Assert
        dbMock.Verify(db => db.BatchUpsertRecordsAsync(It.Is<IEnumerable<FileContext>>(contexts =>
            contexts.Any(c => c.Status == ProcessingStatus.Processed)
        )), Times.Once);

        _mockHashService.Verify(h => h.ComputeContentHashAsync(tempDecryptedFile.FilePath), Times.Once);
        _mockHashService.Verify(h => h.ComputeAudioFingerprintAsync(tempDecryptedFile.FilePath), Times.Once);

        // Check that the decrypted file was moved to the destination
        Assert.Single(Directory.EnumerateFiles(_config.MusicDestDir, "*.mp3"));
    }

    [Fact]
    public async Task EmptyHashResult_IsHandledAsHashFailure()
    {
        using var tempFile = new TemporaryFile("test.mp3", SourceDir).Create();
        _mockHashService.Setup(h => h.ComputeContentHashAsync(tempFile.FilePath)).ReturnsAsync(string.Empty);
        _mockHashService.Setup(h => h.ComputeAudioFingerprintAsync(tempFile.FilePath)).ReturnsAsync("fingerprint");

        var dbMock = new Mock<DatabaseService>(_connection) { CallBase = true };
        var pipeline = new FileProcessingPipeline(_config, _mockDrmPluginLoader.Object, _tempDir, SourceDir);
        var (head, completion) = pipeline.CreatePipeline(dbMock.Object, _mockHashService.Object);

        await head.SendAsync(tempFile.FilePath);
        head.Complete();
        await completion;

        dbMock.Verify(db => db.BatchUpsertRecordsAsync(It.Is<IEnumerable<FileContext>>(contexts =>
            contexts.Any(c => c.Status == ProcessingStatus.HashFailure)
        )), Times.Once);
    }

    [Fact]
    public async Task PreventOverwrite_CreatesNewFileName()
    {
        using var tempFile = new TemporaryFile("test.mp3", SourceDir).Create();
        _mockHashService.Setup(h => h.ComputeContentHashAsync(tempFile.FilePath)).ReturnsAsync("content_hash");
        _mockHashService.Setup(h => h.ComputeAudioFingerprintAsync(tempFile.FilePath)).ReturnsAsync("fingerprint");

        var dbMock = new Mock<DatabaseService>(_connection) { CallBase = true };
        dbMock.Setup(db => db.CheckHashesAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((false, false));

        // Create an existing file in the destination
        var destFilePath = Path.Combine(_config.MusicDestDir, "test.mp3");
        File.WriteAllText(destFilePath, "original");

        var pipeline = new FileProcessingPipeline(_config, _mockDrmPluginLoader.Object, _tempDir, SourceDir);
        var (head, completion) = pipeline.CreatePipeline(dbMock.Object, _mockHashService.Object);

        await head.SendAsync(tempFile.FilePath);
        head.Complete();
        await completion;

        Assert.True(File.Exists(Path.Combine(_config.MusicDestDir, "test (1).mp3")));
    }
    [Fact]
    public async Task PreventOverwrite_WithMultipleExistingFiles_CreatesCorrectlyNumberedFile()
    {
        using var tempFile = new TemporaryFile("test.mp3", SourceDir).Create();
        _mockHashService.Setup(h => h.ComputeContentHashAsync(tempFile.FilePath)).ReturnsAsync("content_hash_2");
        _mockHashService.Setup(h => h.ComputeAudioFingerprintAsync(tempFile.FilePath)).ReturnsAsync("fingerprint_2");

        var dbMock = new Mock<DatabaseService>(_connection) { CallBase = true };
        dbMock.Setup(db => db.CheckHashesAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((false, false));

        // Create multiple existing files in the destination
        var destFilePath = Path.Combine(_config.MusicDestDir, "test.mp3");
        var destFilePath1 = Path.Combine(_config.MusicDestDir, "test (1).mp3");
        File.WriteAllText(destFilePath, "original");
        File.WriteAllText(destFilePath1, "original (1)");

        var pipeline = new FileProcessingPipeline(_config, _mockDrmPluginLoader.Object, _tempDir, SourceDir);
        var (head, completion) = pipeline.CreatePipeline(dbMock.Object, _mockHashService.Object);

        await head.SendAsync(tempFile.FilePath);
        head.Complete();
        await completion;

        Assert.True(File.Exists(Path.Combine(_config.MusicDestDir, "test (2).mp3")));
    }
}
