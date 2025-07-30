using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;

namespace MusicSync.Services;

[ExcludeFromCodeCoverage]
public class DatabaseService : IDisposable
{
    private readonly SqliteConnection _connection;

    private bool _disposed;

    public DatabaseService(string file)
    {
        _connection = new SqliteConnection($"Data Source={file}");
        _connection.Open();
        InitTables();
    }

    private void InitTables()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE IF NOT EXISTS music_hash (
                              id INTEGER PRIMARY KEY AUTOINCREMENT,
                              hash TEXT UNIQUE NOT NULL,
                              first_processed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                          );
                          """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
                          CREATE TABLE IF NOT EXISTS operation_log (
                              id INTEGER PRIMARY KEY AUTOINCREMENT,
                              original_path TEXT NOT NULL,
                              mtime INTEGER NOT NULL,
                              music_hash TEXT,
                              result TEXT NOT NULL,
                              log_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                              UNIQUE (original_path, mtime)
                          );
                          """;
        cmd.ExecuteNonQuery();
    }

    public bool IsMusicHashProcessed(string hash)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM music_hash WHERE hash = $hash";
        cmd.Parameters.AddWithValue("$hash", hash);
        return cmd.ExecuteScalar() != null;
    }

    public void RecordMusicHash(string hash)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO music_hash (hash) VALUES ($hash)";
        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.ExecuteNonQuery();
    }

    public string? FindPreviousResult(string path, long mtime)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT result FROM operation_log WHERE original_path = $p AND mtime = $m";
        cmd.Parameters.AddWithValue("$p", path);
        cmd.Parameters.AddWithValue("$m", mtime);
        return cmd.ExecuteScalar() as string;
    }

    public void LogOperation(string path, long mtime, string? hash, string result, bool logToDb = true)
    {
        if (!logToDb)
        {
            Console.WriteLine($"LOG (Console Only): {Path.GetFileName(path)} -> Result: {result}");
            return;
        }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "INSERT OR IGNORE INTO operation_log (original_path, mtime, music_hash, result) VALUES ($p,$m,$h,$r)";
        cmd.Parameters.AddWithValue("$p", path);
        cmd.Parameters.AddWithValue("$m", mtime);
        cmd.Parameters.AddWithValue("$h", (object?)hash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$r", result);
        cmd.ExecuteNonQuery();
        Console.WriteLine($"LOG (DB): {Path.GetFileName(path)} -> Result: {result}");
    }

    /// <summary>
    /// 实现 IDisposable 接口，释放资源（删除临时目录及其内容）。
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // 释放托管资源
        }

        _connection.Dispose();

        _disposed = true;
    }

    ~DatabaseService()
    {
        Dispose(false);
    }
}
