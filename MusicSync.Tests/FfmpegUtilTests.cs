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
        using var _ = new MockPath("", true);
        Assert.Throws<FileNotFoundException>(FfmpegUtil.CheckFfmpeg);
    }

    [Fact]
    public void CheckFfmpeg_And_GetAudioHash_WorkWithStub()
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

        FfmpegUtil.CheckFfmpeg();

        using var tmpFile = new TemporaryFile("a.txt").Create("hi");
        var hash = FfmpegUtil.GetAudioHash(tmpFile.FilePath);
        var expected = Convert
            .ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(tmpFile.FilePath))).ToLower();
        Assert.Equal($"sha256:{expected}", hash);
    }

    [Fact]
    public void CheckFfmpeg_ThrowsOnNonZeroExitCode()
    {
        using var _ = new MockFfmpeg("""
                                     #!/usr/bin/env bash
                                     if [ "$1" = "-version" ]; then
                                       echo 'error' 1>&2
                                       exit 1
                                     fi
                                     """);
        var ex = Assert.Throws<Exception>(FfmpegUtil.CheckFfmpeg);
        Assert.Contains("exit code", ex.Message);
    }

    [Fact]
    public void GetAudioHash_ReturnsNullOnFailure()
    {
        using var _ = new MockFfmpeg("""
                                     #!/bin/sh
                                     exit 1

                                     """);
        using var tmpFile = new TemporaryFile("a.txt").Create("hi");
        var hash = FfmpegUtil.GetAudioHash(tmpFile.FilePath);
        Assert.Null(hash);
    }

    [Fact]
    public void CheckFfmpeg_ThrowsWhenOutputMissingVersion()
    {
        using var _ = new MockFfmpeg("""
                                     #!/bin/sh

                                     if [ "$1" = "-version" ]; then

                                       echo 'not version'

                                       exit 0
                                     fi
                                     """);
        var ex = Assert.Throws<Exception>(FfmpegUtil.CheckFfmpeg);
        Assert.Contains("Unexpected output", ex.Message);
    }
}
