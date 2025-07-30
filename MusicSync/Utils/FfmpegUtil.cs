using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics.CodeAnalysis;

namespace MusicSync.Utils
{
    [ExcludeFromCodeCoverage]
    public static class FfmpegUtil
    {
        public static string? GetAudioMd5(string filepath)
        {
            var psi = new ProcessStartInfo("ffmpeg", $"-i \"{filepath}\" -vn -f md5 -")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return null;
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(60000);
            var match = Regex.Match(stderr, "MD5=([a-f0-9]{32})");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
