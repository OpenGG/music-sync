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
    public static void CheckFfmpeg()
    {
        var psi = CreateFfmpegProcessStartInfo();
        psi.ArgumentList.Add("-version");

        var (stdout, stderr, exitCode) = RunFfmpegProcess(psi);

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

    /// <summary>
    /// Gets the SHA256 hash of the audio stream from the specified file using ffmpeg.
    /// </summary>
    /// <param name="filepath">The path to the audio file.</param>
    /// <returns>The SHA256 hash prefixed with "sha256:", or null if the hash cannot be obtained.</returns>
    public static string? GetAudioHash(string filepath)
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

        var (stdout, stderr, exitCode) = RunFfmpegProcess(psi);

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

    /// <summary>
    /// Creates a default ProcessStartInfo object for ffmpeg.
    /// </summary>
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

    /// <summary>
    /// Runs an ffmpeg process with the specified ProcessStartInfo and returns its output and exit code.
    /// </summary>
    /// <param name="psi">The ProcessStartInfo for the ffmpeg process.</param>
    /// <param name="timeout">The waiting timeout for the ffmpeg process.</param>
    /// <returns>A tuple containing stdout, stderr, and the exit code.</returns>
    /// <exception cref="Exception">Thrown if the ffmpeg process cannot be started.</exception>
    private static (string stdout, string stderr, int exitCode) RunFfmpegProcess(ProcessStartInfo psi,
        int? timeout = null)
    {
        using var proc = Process.Start(psi);
        if (proc == null)
        {
            throw new Exception(
                $"Failed to start ffmpeg process for command: {psi.FileName} {string.Join(" ", psi.ArgumentList)}");
        }

        // It's generally better to wait for exit before reading streams to avoid deadlocks
        // However, for short-lived processes like these, reading after a timeout is usually safe.
        // For very large outputs, asynchronous reads or separate threads might be needed.
        proc.WaitForExit(timeout ?? 60000); // Wait up to 60 seconds

        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();

        // Ensure the process has truly exited before checking ExitCode
        if (proc.HasExited)
        {
            return (stdout, stderr, proc.ExitCode);
        }

        proc.Kill(); // Terminate if it's still running
        throw new TimeoutException(
            $"ffmpeg process did not exit within 60 seconds for command: {psi.FileName} {string.Join(" ", psi.ArgumentList)}");
    }

    /// <summary>
    /// Regular expression for extracting SHA256 hashes.
    /// Assumes .NET 7+ for GeneratedRegexAttribute.
    /// </summary>
    [GeneratedRegex("SHA256=([a-f0-9]{64})")]
    private static partial Regex GetSha256Regex();
}
