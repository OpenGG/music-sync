using MusicSync.Utils;

namespace MusicSync.Tests;

public class FfmpegUtilTests
{
    [Fact]
    public async Task CheckFfmpeg_ThrowsWhenMissing()
    {
        using var _ = new MockPath("", true);
        await Assert.ThrowsAsync<FileNotFoundException>(FfmpegUtil.CheckFfmpegAsync);
    }

    [Fact]
    public async Task CheckFfmpeg_And_GetAudioHash_WorkWithStub()
    {
        using var _ = new MockFfmpeg("""
                                     #!/usr/bin/env bash

                                     if [ "$1" = "-version" ]; then
                                       echo 'ffmpeg version test';
                                       exit 0;
                                     fi

                                     hash=$(openssl sha256 -r "$2" | cut -d' ' -f1)

                                     echo "SHA256=$hash"
                                     """);

        await FfmpegUtil.CheckFfmpegAsync();

        using var tmpFile = new TemporaryFile("a.txt").Create("hi");
        var hash = await FfmpegUtil.GetAudioHashAsync(tmpFile.FilePath);
        var expected = Convert
            .ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(tmpFile.FilePath))).ToLower();
        Assert.Equal($"sha256:{expected}", hash);
    }

    [Fact]
    public async Task CheckFfmpeg_ThrowsOnNonZeroExitCode()
    {
        using var _ = new MockFfmpeg("""
                                     #!/usr/bin/env bash
                                     if [ "$1" = "-version" ]; then
                                       echo 'error' 1>&2
                                       exit 1
                                     fi
                                     """);
        var ex = await Assert.ThrowsAsync<Exception>(FfmpegUtil.CheckFfmpegAsync);
        Assert.Contains("exit code", ex.Message);
    }

    [Fact]
    public async Task GetAudioHash_ReturnsNullOnFailure()
    {
        using var _ = new MockFfmpeg("""
                                     #!/bin/sh
                                     exit 1

                                     """);
        using var tmpFile = new TemporaryFile("a.txt").Create("hi");
        var hash = await FfmpegUtil.GetAudioHashAsync(tmpFile.FilePath);
        Assert.Null(hash);
    }

    [Fact]
    public async Task CheckFfmpeg_ThrowsWhenOutputMissingVersion()
    {
        using var _ = new MockFfmpeg("""
                                     #!/bin/sh

                                     if [ "$1" = "-version" ]; then

                                       echo 'not version'

                                       exit 0
                                     fi
                                     """);
        var ex = await Assert.ThrowsAsync<Exception>(FfmpegUtil.CheckFfmpegAsync);
        Assert.Contains("Unexpected output", ex.Message);
    }

    [Fact]
    public void GetAudioHash_SyncWrapper_Works()
    {
        using var _ = new MockFfmpeg("""
                                     #!/usr/bin/env bash
                                     hash=$(openssl sha256 -r "$2" | cut -d' ' -f1)
                                     echo "SHA256=$hash"
                                     """);

        using var tmpFile = new TemporaryFile("a.txt").Create("hi");

        // Suppress the warning for the obsolete method for this test
#pragma warning disable CS0618
        var hash = FfmpegUtil.GetAudioHash(tmpFile.FilePath);
#pragma warning restore CS0618

        var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(tmpFile.FilePath))).ToLower();
        Assert.Equal($"sha256:{expected}", hash);
    }
}
