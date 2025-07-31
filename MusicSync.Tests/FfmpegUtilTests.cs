using MusicSync.Utils;
using System;
using System.Diagnostics;
using System.IO;

namespace MusicSync.Tests;

public class FfmpegUtilTests
{
    [Fact]
    public void CheckFfmpeg_ThrowsWhenMissing()
    {
        Assert.Throws<FileNotFoundException>(() => FfmpegUtil.CheckFfmpeg());
    }

    [Fact]
    public void CheckFfmpeg_And_GetAudioHash_WorkWithStub()
    {
        using var tempDir = new TemporaryDirectory().Create();
        var ffmpegPath = Path.Combine(tempDir.DirectoryPath, "ffmpeg");
        var script = "#!/bin/sh\n" +
                     "if [ \"$1\" = \"-version\" ]; then\n" +
                     "  echo 'ffmpeg version test';\n" +
                     "  exit 0;\n" +
                     "fi\n" +
                     "hash=$(sha256sum \"$2\" | cut -d' ' -f1)\n" +
                     "echo \"SHA256=$hash\"\n";
        File.WriteAllText(ffmpegPath, script);
        var chmod = Process.Start("chmod", $"+x {ffmpegPath}");
        chmod?.WaitForExit();

        var originalPath = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", $"{tempDir.DirectoryPath}:{originalPath}");
        try
        {
            FfmpegUtil.CheckFfmpeg();

            using var tmpFile = new TemporaryFile("a.txt").Create("hi");
            var hash = FfmpegUtil.GetAudioHash(tmpFile.FilePath);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var expected = Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(tmpFile.FilePath))).ToLower();
            Assert.Equal($"sha256:{expected}", hash);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void CheckFfmpeg_ThrowsOnNonZeroExitCode()
    {
        using var tempDir = new TemporaryDirectory().Create();
        var ffmpegPath = Path.Combine(tempDir.DirectoryPath, "ffmpeg");
        var script = "#!/bin/sh\n" +
                     "if [ \"$1\" = \"-version\" ]; then\n" +
                     "  echo 'error' 1>&2\n" +
                     "  exit 1\n" +
                     "fi\n";
        File.WriteAllText(ffmpegPath, script);
        Process.Start("chmod", $"+x {ffmpegPath}")?.WaitForExit();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", $"{tempDir.DirectoryPath}:{originalPath}");
        try
        {
            var ex = Assert.Throws<Exception>(() => FfmpegUtil.CheckFfmpeg());
            Assert.Contains("exit code", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void GetAudioHash_ReturnsNullOnFailure()
    {
        using var tempDir = new TemporaryDirectory().Create();
        var ffmpegPath = Path.Combine(tempDir.DirectoryPath, "ffmpeg");
        var script = "#!/bin/sh\nexit 1\n";
        File.WriteAllText(ffmpegPath, script);
        Process.Start("chmod", $"+x {ffmpegPath}")?.WaitForExit();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", $"{tempDir.DirectoryPath}:{originalPath}");
        try
        {
            using var tmpFile = new TemporaryFile("a.txt").Create("hi");
            var hash = FfmpegUtil.GetAudioHash(tmpFile.FilePath);
            Assert.Null(hash);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void CheckFfmpeg_ThrowsWhenOutputMissingVersion()
    {
        using var tempDir = new TemporaryDirectory().Create();
        var ffmpegPath = Path.Combine(tempDir.DirectoryPath, "ffmpeg");
        var script = "#!/bin/sh\n" +
                     "if [ \"$1\" = \"-version\" ]; then\n" +
                     "  echo 'not version'\n" +
                     "  exit 0\n" +
                     "fi\n";
        File.WriteAllText(ffmpegPath, script);
        Process.Start("chmod", $"+x {ffmpegPath}")?.WaitForExit();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", $"{tempDir.DirectoryPath}:{originalPath}");
        try
        {
            var ex = Assert.Throws<Exception>(() => FfmpegUtil.CheckFfmpeg());
            Assert.Contains("Unexpected output", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }
}

