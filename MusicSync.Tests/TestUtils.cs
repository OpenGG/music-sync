using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MusicSync.Utils;

namespace MusicSync.Tests;

public static class TestUtils
{
    private const string
        ResourceName = "MusicSync.Tests.Fixtures.silent_audio.mp3"; // Adjust to your actual namespace and path

    public static byte[] GetMp3Bytes()
    {
        // Get the current assembly where the resource is embedded
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"{ResourceName} not found");
        }

        // Now you can read from the stream (e.g., convert to byte array)
        using var memoryStream = new MemoryStream();

        stream.CopyTo(memoryStream);
        var mp3Data = memoryStream.ToArray();

        return mp3Data;
    }

    public static void SetExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead);
    }
}

public sealed class MockPath : IDisposable
{
    private readonly string _originalPath;
    private bool _disposed;

    public MockPath(string path, bool cleanMode = false)
    {
        _originalPath = Environment.GetEnvironmentVariable("PATH") ?? "";

        var newPath = cleanMode == true ? path : $"{path}:{_originalPath}";

        Environment.SetEnvironmentVariable("PATH", newPath);
    }

    // Public implementation of Dispose pattern.
    public void Dispose()
    {
        Dispose(true);
        // Tell the GC not to call the finalizer, since we've already cleaned up.
        GC.SuppressFinalize(this);
    }

    // Protected virtual method to handle the actual cleanup.
    // 'disposing' will be 'true' when called from the public Dispose method.
    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // 释放托管资源
        }

        // Restore the PATH variable first.
        Environment.SetEnvironmentVariable("PATH", _originalPath);

        _disposed = true;
    }

    [ExcludeFromCodeCoverage]
    ~MockPath()
    {
        Dispose(false);
    }
}

public sealed class MockFfmpeg : IDisposable
{
    private MockPath? _mockPath;
    private TemporaryDirectory? _tempDir;
    private TemporaryFile? _ffmpegFile;
    private bool _disposed;

    public MockFfmpeg(string content)
    {
        _tempDir = new TemporaryDirectory().Create();
        _ffmpegFile = _tempDir.CreateTemporaryFile("ffmpeg", content);
        TestUtils.SetExecutable(_ffmpegFile.FilePath);

        _mockPath = new MockPath(_tempDir.DirectoryPath);
    }

    // Public implementation of Dispose pattern.
    public void Dispose()
    {
        Dispose(true);
        // Tell the GC not to call the finalizer, since we've already cleaned up.
        GC.SuppressFinalize(this);
    }

    // Protected virtual method to handle the actual cleanup.
    // 'disposing' will be 'true' when called from the public Dispose method.
    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // 释放托管资源
        }


        // Restore the PATH variable first.
        if (disposing)
        {
            // Dispose managed resources
            _mockPath?.Dispose();
            _mockPath = null;

            _ffmpegFile?.Dispose();
            _ffmpegFile = null;

            _tempDir?.Dispose();
            _tempDir = null;
        }

        _disposed = true;
    }

    [ExcludeFromCodeCoverage]
    ~MockFfmpeg()
    {
        Dispose(false);
    }
}
