using System.Diagnostics.CodeAnalysis;

namespace MusicSync.Utils;

/// <summary>
/// 表示一个临时目录，并在 Dispose 时自动删除。
/// 无参构造函数会自动创建一个唯一的临时目录。
/// </summary>
public sealed class TemporaryDirectory : IDisposable
{
    /// <summary>
    /// 获取临时目录的完整路径。
    /// </summary>
    public string DirectoryPath { get; }

    private bool _disposed;

    /// <summary>
    /// 初始化 TemporaryDirectory 的新实例。此构造函数会**自动在系统临时路径下创建一个唯一的临时目录**。
    /// </summary>
    public TemporaryDirectory(string? baseDir = null)
    {
        baseDir ??= Path.GetTempPath();

        DirectoryPath = Path.Join(baseDir, $"music-sync-{Path.GetRandomFileName()}");
    }

    public TemporaryDirectory Create()
    {
        Directory.CreateDirectory(DirectoryPath); // 自动创建目录
        return this;
    }

    public TemporaryDirectory CreateTemporaryDirectory()
    {
        return new TemporaryDirectory(DirectoryPath);
    }

    public TemporaryFile CreateTemporaryFile(string filename, byte[] content)
    {
        return new TemporaryFile(filename, DirectoryPath).Create(content);
    }

    public TemporaryFile CreateTemporaryFile(string filename, string? content)
    {
        return new TemporaryFile(filename, DirectoryPath).Create(content);
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

        // 释放非托管资源（删除目录）
        if (Directory.Exists(DirectoryPath))
        {
            try
            {
                // true 表示递归删除所有子文件和子目录
                Directory.Delete(DirectoryPath, true);
                // Console.WriteLine($"[TemporaryDirectory] 已删除：{DirectoryPath}"); // 调试用
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[TemporaryDirectory] 错误：删除临时目录失败 {DirectoryPath}: {ex.Message}");
            }
        }

        _disposed = true;
    }

    [ExcludeFromCodeCoverage]
    ~TemporaryDirectory()
    {
        Dispose(false);
    }
}
