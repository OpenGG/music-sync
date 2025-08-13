using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using MusicSync.Models;

namespace MusicSync.Services;

public class DatabaseService : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly bool _externalConnection;
    private bool _disposed;

    public DatabaseService(IOptions<Config> options) : this(new SqliteConnection($"Data Source={options.Value.DatabaseFile}"))
    {
        _externalConnection = false;
    }

    public DatabaseService(string file) : this(new SqliteConnection($"Data Source={file}"))
    {
        _externalConnection = false;
    }

    public DatabaseService(SqliteConnection connection)
    {
        _connection = connection;
        _externalConnection = true;
    }

    public async Task InitializeDatabaseAsync()
    {
        await _connection.OpenAsync();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE IF NOT EXISTS FileRecords (
                              Id INTEGER PRIMARY KEY AUTOINCREMENT,
                              AbsolutePath TEXT NOT NULL,
                              MTime INTEGER NOT NULL,
                              ContentHash TEXT,
                              AudioFingerprint TEXT,
                              Status INTEGER NOT NULL,
                              LastProcessedAt DATETIME NOT NULL
                          );

                          DROP INDEX IF EXISTS IDX_FileRecords_Path_MTime;
                          CREATE UNIQUE INDEX IF NOT EXISTS IDX_FileRecords_Path ON FileRecords(AbsolutePath);
                          CREATE INDEX IF NOT EXISTS IDX_FileRecords_ContentHash ON FileRecords(ContentHash);
                          CREATE INDEX IF NOT EXISTS IDX_FileRecords_AudioFingerprint ON FileRecords(AudioFingerprint);
                          """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> CheckMetadataExistsAsync(string absolutePath, long mTime)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM FileRecords WHERE AbsolutePath = $path AND MTime = $mtime";
        cmd.Parameters.AddWithValue("$path", absolutePath);
        cmd.Parameters.AddWithValue("$mtime", mTime);
        return await cmd.ExecuteScalarAsync() != null;
    }

    public virtual async Task<(bool contentHashExists, bool audioFingerprintExists)> CheckHashesAsync(string contentHash, string audioFingerprint)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              (SELECT 1 FROM FileRecords WHERE ContentHash = $ch) as ContentHashExists,
                              (SELECT 1 FROM FileRecords WHERE AudioFingerprint = $af) as AudioFingerprintExists
                          """;
        cmd.Parameters.AddWithValue("$ch", (object?)contentHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$af", (object?)audioFingerprint ?? DBNull.Value);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var contentHashExists = !reader.IsDBNull(0) && reader.GetInt32(0) == 1;
            var audioFingerprintExists = !reader.IsDBNull(1) && reader.GetInt32(1) == 1;
            return (contentHashExists, audioFingerprintExists);
        }

        return (false, false);
    }

    public virtual async Task BatchUpsertRecordsAsync(IEnumerable<FileContext> contexts)
    {
        await using var transaction = (SqliteTransaction)await _connection.BeginTransactionAsync();

        foreach (var context in contexts)
        {
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = transaction;

            cmd.CommandText = """
                INSERT INTO FileRecords (AbsolutePath, MTime, ContentHash, AudioFingerprint, Status, LastProcessedAt)
                VALUES ($path, $mtime, $ch, $af, $status, $lpa)
                ON CONFLICT(AbsolutePath) DO UPDATE SET
                    MTime = excluded.MTime,
                    ContentHash = excluded.ContentHash,
                    AudioFingerprint = excluded.AudioFingerprint,
                    Status = excluded.Status,
                    LastProcessedAt = excluded.LastProcessedAt;
                """;

            cmd.Parameters.AddWithValue("$path", context.FilePath);
            cmd.Parameters.AddWithValue("$mtime", context.MTime);
            cmd.Parameters.AddWithValue("$ch", (object?)context.ContentHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$af", (object?)context.AudioFingerprint ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$status", (int)context.Status);
            cmd.Parameters.AddWithValue("$lpa", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // Release managed resources
        }

        if (!_externalConnection)
        {
            await _connection.DisposeAsync();
        }

        _disposed = true;
    }

    ~DatabaseService()
    {
        DisposeAsync(false).GetAwaiter().GetResult();
    }
}
