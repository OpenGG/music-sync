using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MusicSync.Models;
using MusicSync.Utils;

using System.Diagnostics.CodeAnalysis;

namespace MusicSync.Services
{
    [ExcludeFromCodeCoverage]
    public class MusicFileProcessor
    {
        private readonly DatabaseService _db;
        private readonly string _incomingDir;
        private readonly string[] _supportedExtensions;
        private readonly DrmPluginLoader _pluginLoader;

        public MusicFileProcessor(DatabaseService db, string incomingDir, string[] supportedExtensions, DrmPluginLoader pluginLoader)
        {
            _db = db;
            _incomingDir = incomingDir;
            _supportedExtensions = supportedExtensions;
            _pluginLoader = pluginLoader;
        }

        public void ProcessFile(string path, string sourceDir)
        {
            long mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();
            var prev = _db.FindPreviousResult(path, mtime);
            if (prev != null && new[] { "copy_success", "dedrm_success" }.Contains(prev))
            {
                _db.LogOperation(path, mtime, null, "skip_path_mtime_exists", false);
                return;
            }
            string name = Path.GetFileNameWithoutExtension(path);
            string relativePath = Path.GetRelativePath(sourceDir, path);

            var plugin = _pluginLoader.Resolve(path);
            if (plugin != null)
            {
                HandleDrmFile(path, mtime, relativePath, name, plugin);
            }
            else if (_supportedExtensions.Contains(Path.GetExtension(path).ToLower()))
            {
                HandleRegularFile(path, mtime, relativePath, name);
            }
            else
            {
                _db.LogOperation(path, mtime, null, "unsupported_type");
                Console.WriteLine($"Skipping unsupported file type: {path}");
            }
        }

        private static string ComputeHash(string file, HashAlgorithm algorithm)
        {
            return HashUtil.ComputeHash(file, algorithm);
        }

        private string? GetMusicMd5(string filepath)
        {
            try
            {
                var md5 = FfmpegUtil.GetAudioMd5(filepath);
                if (!string.IsNullOrEmpty(md5)) return md5;
                return ComputeHash(filepath, MD5.Create());
            }
            catch
            {
                return ComputeHash(filepath, MD5.Create());
            }
        }

        private void HandleRegularFile(string path, long mtime, string relativePath, string name)
        {
            string targetDir = Path.Combine(_incomingDir, Path.GetDirectoryName(relativePath) ?? string.Empty);
            Directory.CreateDirectory(targetDir);
            string dest = Path.Combine(targetDir, Path.GetFileName(path));
            string? md5 = GetMusicMd5(path);
            if (md5 == null)
            {
                _db.LogOperation(path, mtime, null, "md5_fail_copy");
                return;
            }
            if (_db.IsMusicHashProcessed(md5))
            {
                _db.LogOperation(path, mtime, md5, "skip_music_hash_exists", false);
                return;
            }
            File.Copy(path, dest, true);
            _db.RecordMusicHash(md5);
            _db.LogOperation(path, mtime, md5, "copy_success");
        }

        private void HandleDrmFile(string path, long mtime, string relativePath, string name, DrmPlugin plugin)
        {
            var found = plugin.Decrypt(path, _supportedExtensions);
            if (found == null)
            {
                _db.LogOperation(path, mtime, null, $"dedrm_fail_plugin_error_{plugin.Name}");
                return;
            }

            string? md5 = GetMusicMd5(found);
            if (md5 == null)
            {
                _db.LogOperation(path, mtime, null, "md5_fail_dedrm");
                return;
            }

            if (_db.IsMusicHashProcessed(md5))
            {
                _db.LogOperation(path, mtime, md5, "skip_music_hash_exists", false);
                return;
            }

            string targetDir = Path.Combine(_incomingDir, Path.GetDirectoryName(relativePath) ?? string.Empty);
            Directory.CreateDirectory(targetDir);
            string finalPath = Path.Combine(targetDir, name + Path.GetExtension(found));
            File.Move(found, finalPath, true);
            _db.RecordMusicHash(md5);
            _db.LogOperation(path, mtime, md5, "dedrm_success");
        }
    }
}
