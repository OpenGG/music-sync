using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MusicSync.Utils;
public static partial class FfmpegUtil
{
    /// <summary>
    /// Checks if ffmpeg is installed and accessible by running `ffmpeg -version`.
    /// Throws an exception if ffmpeg is not found or the version check fails.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown when ffmpeg executable is not found.</exception>
    /// <exception cref="Exception">Thrown when ffmpeg version check fails or an unexpected error occurs.</exception>
    public static async Task CheckFfmpegAsync()
    {
        var psi = CreateFfmpegProcessStartInfo();
        psi.ArgumentList.Add("-version");

        var (stdout, stderr, exitCode) = await RunFfmpegProcessAsync(psi);

        if (exitCode != 0)
        {
            if (stderr.Contains("not found") || stderr.Contains("command not found"))
            {
                throw new FileNotFoundException(
                    "ffmpeg executable not found. Please ensure ffmpeg is installed and added to your system's PATH.");
            }

            throw new Exception($"ffmpeg version check failed with exit code {exitCode}.\nStderr: {stderr}");
        }

        var content = $"{stdout}\n{stderr}";
        if (!content.Contains("ffmpeg version"))
        {
            throw new Exception($"ffmpeg version check failed: Unexpected output.\nOutput: {content}");
        }
    }

    public static async Task<string?> GetAudioHashAsync(string filepath)
    {
        var psi = CreateFfmpegProcessStartInfo();
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(filepath);
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:a"); // Map only audio stream
        psi.ArgumentList.Add("-vn"); // No video
        psi.ArgumentList.Add("-sn"); // No subtitles
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("hash");
        psi.ArgumentList.Add("-hash");
        psi.ArgumentList.Add("sha256");
        psi.ArgumentList.Add("-hide_banner"); // Hide ffmpeg banner
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("warning"); // Only show warnings and errors
        psi.ArgumentList.Add("-"); // Output to stdout

        var (stdout, stderr, exitCode) = await RunFfmpegProcessAsync(psi);

        if (exitCode != 0)
        {
            // ffmpeg failed, e.g., file not found, no audio stream, etc.
            Console.Error.WriteLine(
                $"Error getting audio hash for '{filepath}': Exit code {exitCode}.\nStderr: {stderr}");
            return null;
        }

        var match = GetSha256Regex().Match($"{stdout}\n{stderr}");
        return match.Success ? $"sha256:{match.Groups[1].Value}" : null;
    }



    [Obsolete("Use GetAudioHashAsync instead.")]
    public static string? GetAudioHash(string filepath) => GetAudioHashAsync(filepath).GetAwaiter().GetResult();

    private static ProcessStartInfo CreateFfmpegProcessStartInfo()
    {
        return new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static async Task<(string stdout, string stderr, int exitCode)> RunFfmpegProcessAsync(ProcessStartInfo psi, CancellationToken cancellationToken = default)
    {
        try
        {
            using var proc = Process.Start(psi);
            if (proc == null)
            {
                throw new Exception(
                    $"Failed to start ffmpeg process for command: {psi.FileName} {string.Join(" ", psi.ArgumentList)}");
            }

            await proc.WaitForExitAsync(cancellationToken);

            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);

            return (stdout, stderr, proc.ExitCode);
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 2)
        {
            throw new FileNotFoundException(
                "ffmpeg executable not found. Please ensure ffmpeg is installed and added to your system's PATH.", e);
        }
    }

    /// <summary>
    /// Regular expression for extracting SHA256 hashes.
    /// Assumes .NET 7+ for GeneratedRegexAttribute.
    /// </summary>
    [GeneratedRegex("SHA256=([a-f0-9]{64})")]
    private static partial Regex GetSha256Regex();
}
