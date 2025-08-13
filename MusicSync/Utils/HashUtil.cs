using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Blake3;

namespace MusicSync.Utils;

[ExcludeFromCodeCoverage]
public static class HashUtil
{
    public static async Task<string> ComputeHashAsync(string file, HashAlgorithm algorithm)
    {
        await using var stream = File.OpenRead(file);
        var hashBytes = await algorithm.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes).ToLower();
    }

    public static async Task<string> ComputeBlake3HashAsync(string filePath)
    {
        var hasher = Hasher.New();
        await using (var stream = File.OpenRead(filePath))
        {
            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                hasher.Update(buffer.AsSpan(0, bytesRead));
            }
        }
        var hash = hasher.Finalize();
        return hash.ToString().ToLower();
    }
}
