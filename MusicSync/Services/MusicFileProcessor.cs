using System.Security.Cryptography;
using MusicSync.Models;
using MusicSync.Plugins;
using MusicSync.Utils;

namespace MusicSync.Services;

public class MusicFileProcessor(
    DatabaseService db,
    Config config,
    TemporaryDirectory rootTempDir,
    DrmPluginLoader pluginLoader)
{
    private static readonly string[] SuccessResults = ["copy_success", "dedrm_success"];

    private readonly string[] _mediaExtensions = config.MusicExtensions.Select(e => e.ToLower()).ToArray();

    public void ProcessFile(string path, string sourceDir)
    {
        var mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();
        var prev = db.FindPreviousResult(path, mtime);
        if (prev != null && SuccessResults.Contains(prev))
        {
            db.LogOperation(path, mtime, null, "skip_path_mtime_exists", false);
            return;
        }

        var name = Path.GetFileNameWithoutExtension(path);
        var relativePath = Path.GetRelativePath(sourceDir, path);

        var plugin = pluginLoader.Resolve(path);
        if (plugin != null)
        {
            HandleDrmFile(path, mtime, relativePath, name, plugin);
        }
        else if (_mediaExtensions.Contains(Path.GetExtension(path).ToLower()))
        {
            HandleRegularFile(path, mtime, relativePath);
        }
        else
        {
            db.LogOperation(path, mtime, null, "unsupported_type");
            Console.Error.WriteLine($"Skipping unsupported file type: {path}");
        }
    }

    // private static string ComputeHash(string file, HashAlgorithm algorithm)
    // {
    //     return HashUtil.ComputeHash(file, algorithm);
    // }

    private static string? GetMusicHash(string filepath)
    {
        try
        {
            var hash = FfmpegUtil.GetAudioHash(filepath);
            if (!string.IsNullOrEmpty(hash))
            {
                return hash;
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error GetMusicHash(): {e}");
        }

        try
        {
            using var sha = SHA256.Create();
            return $"sha256:{HashUtil.ComputeHash(filepath, sha)}";
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error GetMusicHash fallback: {e}");
            return null;
        }
    }

    private void HandleRegularFile(string path, long mtime, string relativePath)
    {
        var targetDir = Path.Join(config.MusicDestDir, Path.GetDirectoryName(relativePath) ?? string.Empty);
        Directory.CreateDirectory(targetDir);
        var dest = Path.Join(targetDir, Path.GetFileName(path));
        var hash = GetMusicHash(path);
        if (hash == null)
        {
            db.LogOperation(path, mtime, null, "hash_fail_copy");
            return;
        }

        if (db.IsMusicHashProcessed(hash))
        {
            db.LogOperation(path, mtime, hash, "skip_music_hash_exists", false);
            return;
        }

        var finalPath = PreventOverwrite(dest);
        File.Copy(path, finalPath, true);
        db.RecordMusicHash(hash);
        db.LogOperation(path, mtime, hash, "copy_success");
    }

    private void HandleDrmFile(string path, long mtime, string relativePath, string name, DrmPlugin plugin)
    {
        using var tempDir = rootTempDir.CreateTemporaryDirectory();

        var found = plugin.Decrypt(path, tempDir, _mediaExtensions);
        if (found == null)
        {
            db.LogOperation(path, mtime, null, $"dedrm_fail_plugin_error_{plugin.Name}");
            return;
        }

        var hash = GetMusicHash(found);
        if (hash == null)
        {
            db.LogOperation(path, mtime, null, "hash_fail_dedrm");
            return;
        }

        if (db.IsMusicHashProcessed(hash))
        {
            db.LogOperation(path, mtime, hash, "skip_music_hash_exists", false);
            return;
        }

        var targetDir = Path.Join(config.MusicDestDir, Path.GetDirectoryName(relativePath) ?? string.Empty);
        Directory.CreateDirectory(targetDir);
        var destPath = Path.Join(targetDir, name + Path.GetExtension(found));
        var finalPath = PreventOverwrite(destPath);
        File.Move(found, finalPath, true);
        db.RecordMusicHash(hash);
        db.LogOperation(path, mtime, hash, "dedrm_success");
    }

    private static string PreventOverwrite(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        for (var i = 0; ; ++i)
        {
            var uniqName =
                $"{Path.GetFileNameWithoutExtension(path)}.{i}{Path.GetExtension(path)}";

            var uniqPath =
                Path.Join(Path.GetDirectoryName(path), uniqName);

            if (!File.Exists(uniqPath))
            {
                return uniqPath;
            }
        }
    }
}
