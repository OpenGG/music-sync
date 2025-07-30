using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;

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