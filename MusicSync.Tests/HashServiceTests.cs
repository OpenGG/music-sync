using Xunit;
using MusicSync.Services;
using System.IO;
using System.Threading.Tasks;
using MusicSync.Utils;

namespace MusicSync.Tests;

public class HashServiceTests
{
    private readonly HashService _hashService = new();

    [Fact]
    public async Task ComputeContentHashAsync_ShouldReturnCorrectBlake3Hash()
    {
        using var tempFile = new TemporaryFile("test.txt").Create("test content");

        var expectedHash = "ead3df8af4aece7792496936f83b6b6d191a7f256585ce6b6028db161278017e";
        var actualHash = await _hashService.ComputeContentHashAsync(tempFile.FilePath);

        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public async Task ComputeAudioFingerprintAsync_ShouldReturnHashForValidAudio()
    {
        var mp3Bytes = TestUtils.GetMp3Bytes();
        using var tempFile = new TemporaryFile("silent.mp3").Create(mp3Bytes);

        var fingerprint = await _hashService.ComputeAudioFingerprintAsync(tempFile.FilePath);

        Assert.NotNull(fingerprint);
        Assert.StartsWith("sha256:", fingerprint);
        Assert.Equal(64 + 7, fingerprint.Length); // "sha256:" + 64 hex chars
    }

    [Fact]
    public async Task ComputeAudioFingerprintAsync_ShouldReturnNullForInvalidFile()
    {
        using var tempFile = new TemporaryFile("not_audio.txt").Create("this is not an audio file");

        var fingerprint = await _hashService.ComputeAudioFingerprintAsync(tempFile.FilePath);

        Assert.Null(fingerprint);
    }
}
