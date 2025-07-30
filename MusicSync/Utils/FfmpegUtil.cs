using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MusicSync.Utils;

public static partial class FfmpegUtil
{
    public static string? GetAudioHash(string filepath)
    {
        var psi = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(filepath);
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:a");
        psi.ArgumentList.Add("-vn");
        psi.ArgumentList.Add("-sn");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("hash");
        psi.ArgumentList.Add("-hash");
        psi.ArgumentList.Add("sha256");
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("warning");
        psi.ArgumentList.Add("-");

        using var proc = Process.Start(psi);
        if (proc == null) return null;
        proc.WaitForExit(60000);
        var stderr = proc.StandardError.ReadToEnd();
        var stdout = proc.StandardOutput.ReadToEnd();
        var match = GetSha256Regex().Match($"{stdout}\n${stderr}");
        return match.Success ? $"sha256:{match.Groups[1].Value}" : null;
    }

    // 使用 GeneratedRegexAttribute 生成正则表达式
    // 确保你的项目目标框架是 .NET 7 或更高版本，或者安装了相应的 NuGet 包
    // (<PackageReference Include="System.Text.RegularExpressions" Version="X.Y.Z" /> 如果需要)
    [GeneratedRegex("SHA256=([a-f0-9]{64})")]
    private static partial Regex GetSha256Regex(); // 声明为 partial 方法
}
