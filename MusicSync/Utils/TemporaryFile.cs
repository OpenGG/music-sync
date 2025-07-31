using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace MusicSync.Utils;

/// <summary>
/// 表示一个临时文件，并在 Dispose 时自动删除。
/// 无参构造函数会自动创建一个唯一的零字节临时文件。
/// </summary>
public class TemporaryFile : IDisposable
{
    private static readonly string CommonBaseDir =
        Path.Join(Path.GetTempPath(), $"music-sync-files-{Path.GetRandomFileName()}");

    /// <summary>
    /// 获取临时文件的完整路径。
    /// </summary>
    public string FilePath { get; }


    private bool _disposed;

    private readonly string _baseDir;

    /// <summary>
    /// 初始化 TemporaryFile 的新实例。此构造函数会**自动创建一个唯一的零字节临时文件**。
    /// 文件路径由 Path.GetTempFileName() 生成。
    /// </summary>
    public TemporaryFile(string filename, string? baseDir = null)
    {
        _baseDir = baseDir ?? CommonBaseDir;

        FilePath = Path.Join(_baseDir, filename);
    }

    public TemporaryFile Create(byte[] content)
    {
        Directory.CreateDirectory(_baseDir);
        File.WriteAllBytes(FilePath, content);
        return this;
    }

    public TemporaryFile Create(string? content = null)
    {
        Directory.CreateDirectory(_baseDir);
        File.WriteAllText(FilePath, content ?? "");
        return this;
    }

    /// <summary>
    /// 实现 IDisposable 接口，释放资源（删除临时文件）。
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

        // 释放非托管资源（删除文件）
        if (File.Exists(FilePath))
        {
            try
            {
                File.Delete(FilePath);
                // Console.WriteLine($"[TemporaryFile] 已删除：{FilePath}"); // 调试用

                if (_baseDir == CommonBaseDir && !Directory.EnumerateFileSystemEntries(CommonBaseDir).Any())
                {
                    Directory.Delete(CommonBaseDir, true);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[TemporaryFile] 错误：删除临时文件失败 {FilePath}: {ex.Message}");
            }
        }

        _disposed = true;
    }

    [ExcludeFromCodeCoverage]
    ~TemporaryFile()
    {
        Dispose(false);
    }
}
