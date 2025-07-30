using MusicSync.Plugins;
using MusicSync.Utils;
using System.Diagnostics.CodeAnalysis;

namespace MusicSync.Services;

[ExcludeFromCodeCoverage]
public class MusicFileProcessor(
    DatabaseService db,
    string incomingDir,
    string[] supportedExtensions,
    DrmPluginLoader pluginLoader)
{
    private static readonly string[] SourceArray = ["copy_success", "dedrm_success"];

    public void ProcessFile(string path, string sourceDir)
    {
        var mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();
        var prev = db.FindPreviousResult(path, mtime);
        if (prev != null && SourceArray.Contains(prev))
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
        else if (supportedExtensions.Contains(Path.GetExtension(path).ToLower()))
        {
            HandleRegularFile(path, mtime, relativePath);
        }
        else
        {
            db.LogOperation(path, mtime, null, "unsupported_type");
            Console.WriteLine($"Skipping unsupported file type: {path}");
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
            var md5 = FfmpegUtil.GetAudioMd5(filepath);
            return md5;
            // return string.IsNullOrEmpty(md5) ? ComputeHash(filepath, MD5.Create()) : md5;
        }
        catch
        {
            return null;
            // return ComputeHash(filepath, MD5.Create());
        }
    }

    private void HandleRegularFile(string path, long mtime, string relativePath)
    {
        var targetDir = Path.Combine(incomingDir, Path.GetDirectoryName(relativePath) ?? string.Empty);
        Directory.CreateDirectory(targetDir);
        var dest = Path.Combine(targetDir, Path.GetFileName(path));
        var md5 = GetMusicHash(path);
        if (md5 == null)
        {
            db.LogOperation(path, mtime, null, "md5_fail_copy");
            return;
        }

        if (db.IsMusicHashProcessed(md5))
        {
            db.LogOperation(path, mtime, md5, "skip_music_hash_exists", false);
            return;
        }

        File.Copy(path, dest, true);
        db.RecordMusicHash(md5);
        db.LogOperation(path, mtime, md5, "copy_success");
    }

    private void HandleDrmFile(string path, long mtime, string relativePath, string name, DrmPlugin plugin)
    {
        var found = plugin.Decrypt(path, supportedExtensions);
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

        var targetDir = Path.Combine(incomingDir, Path.GetDirectoryName(relativePath) ?? string.Empty);
        Directory.CreateDirectory(targetDir);
        var finalPath = Path.Combine(targetDir, name + Path.GetExtension(found));
        File.Move(found, finalPath, true);
        db.RecordMusicHash(hash);
        db.LogOperation(path, mtime, hash, "dedrm_success");
    }
}