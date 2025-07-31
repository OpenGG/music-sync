using MusicSync.Utils;
using System.IO;

namespace MusicSync.Tests;

public class TemporaryUtilsTests
{
    [Fact]
    public void TemporaryDirectory_DisposeWithoutCreate_DoesNotThrow()
    {
        var dir = new TemporaryDirectory();
        dir.Dispose();
        Assert.False(Directory.Exists(dir.DirectoryPath));
        dir.Dispose();
    }

    [Fact]
    public void TemporaryFile_DisposeWithoutCreate_DoesNotThrow()
    {
        var file = new TemporaryFile("temp.txt");
        file.Dispose();
        Assert.False(File.Exists(file.FilePath));
        file.Dispose();
    }

    [Fact]
    public void TemporaryDirectory_CreateAndCreateFile_CleansUp()
    {
        string dirPath;
        using (var dir = new TemporaryDirectory().Create())
        {
            dirPath = dir.DirectoryPath;
            Assert.True(Directory.Exists(dirPath));
            using var file = dir.CreateTemporaryFile("data.txt", "hello");
            Assert.True(File.Exists(file.FilePath));
        }
        Assert.False(Directory.Exists(dirPath));
    }

    [Fact]
    public void TemporaryFile_CreateWritesContentAndDeletesOnDispose()
    {
        string filePath;
        using (var file = new TemporaryFile("data.txt").Create("test"))
        {
            filePath = file.FilePath;
            Assert.True(File.Exists(filePath));
            Assert.Equal("test", File.ReadAllText(filePath));
        }
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void TemporaryDirectory_CreateTemporaryDirectory_Works()
    {
        string nestedPath;
        using (var dir = new TemporaryDirectory().Create())
        {
            using var nested = dir.CreateTemporaryDirectory().Create();
            nestedPath = nested.DirectoryPath;
            Assert.True(Directory.Exists(nestedPath));
        }
        Assert.False(Directory.Exists(nestedPath));
    }

    [Fact]
    public void TemporaryFile_CreateByteContent_Works()
    {
        byte[] data = [1, 2, 3];
        string filePath;
        using (var file = new TemporaryFile("bin.dat").Create(data))
        {
            filePath = file.FilePath;
            Assert.True(File.Exists(filePath));
            Assert.Equal(data, File.ReadAllBytes(filePath));
        }
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void TemporaryDirectory_CreateTemporaryFileWithBytes_Works()
    {
        byte[] data = [4, 5];
        string filePath;
        using (var dir = new TemporaryDirectory().Create())
        {
            using var file = dir.CreateTemporaryFile("bytes.bin", data);
            filePath = file.FilePath;
            Assert.True(File.Exists(filePath));
            Assert.Equal(data, File.ReadAllBytes(filePath));
        }
        Assert.False(File.Exists(filePath));
    }
}

