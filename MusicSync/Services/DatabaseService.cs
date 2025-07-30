using System;
using Microsoft.Data.Sqlite;

using System.Diagnostics.CodeAnalysis;

namespace MusicSync.Services
{
    [ExcludeFromCodeCoverage]
    public class DatabaseService : IDisposable
    {
        private readonly SqliteConnection _connection;
        public DatabaseService(string file)
        {
            _connection = new SqliteConnection($"Data Source={file}");
            _connection.Open();
            InitTables();
        }

        private void InitTables()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS music_hash (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                md5_hash TEXT UNIQUE NOT NULL,
                first_processed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );";
            cmd.ExecuteNonQuery();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS operation_log (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                original_path TEXT NOT NULL,
                mtime INTEGER NOT NULL,
                music_md5_hash TEXT,
                result TEXT NOT NULL,
                log_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                UNIQUE (original_path, mtime)
            );";
            cmd.ExecuteNonQuery();
        }

        public bool IsMusicHashProcessed(string md5)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM music_hash WHERE md5_hash = $hash";
            cmd.Parameters.AddWithValue("$hash", md5);
            return cmd.ExecuteScalar() != null;
        }

        public void RecordMusicHash(string md5)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO music_hash (md5_hash) VALUES ($hash)";
            cmd.Parameters.AddWithValue("$hash", md5);
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

        public void LogOperation(string path, long mtime, string? md5, string result, bool logToDb = true)
        {
            if (!logToDb)
            {
                Console.WriteLine($"LOG (Console Only): {System.IO.Path.GetFileName(path)} -> Result: {result}");
                return;
            }
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO operation_log (original_path, mtime, music_md5_hash, result) VALUES ($p,$m,$h,$r)";
            cmd.Parameters.AddWithValue("$p", path);
            cmd.Parameters.AddWithValue("$m", mtime);
            cmd.Parameters.AddWithValue("$h", (object?)md5 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$r", result);
            cmd.ExecuteNonQuery();
            Console.WriteLine($"LOG (DB): {System.IO.Path.GetFileName(path)} -> Result: {result}");
        }

        public void Dispose() => _connection.Dispose();
    }
}
