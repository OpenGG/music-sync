using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace MusicSync.Utils;

[ExcludeFromCodeCoverage]
public static class HashUtil
{
    public static string ComputeHash(string file, HashAlgorithm algorithm)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(algorithm.ComputeHash(stream)).ToLower();
    }
}
