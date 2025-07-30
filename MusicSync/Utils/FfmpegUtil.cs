using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MusicSync.Utils;

public static partial class FfmpegUtil
{
    public static string? GetAudioMd5(string filepath)
    {
        var psi = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(filepath); // filepath 直接作为参数传入，无需手动加引号
        psi.ArgumentList.Add("-vn");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("md5");
        psi.ArgumentList.Add("-");

        using var proc = Process.Start(psi);
        if (proc == null) return null;
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(60000);
        var match = GetMd5Regex().Match(stderr);
        return match.Success ? match.Groups[1].Value : null;
    }

    // 使用 GeneratedRegexAttribute 生成正则表达式
    // 确保你的项目目标框架是 .NET 7 或更高版本，或者安装了相应的 NuGet 包
    // (<PackageReference Include="System.Text.RegularExpressions" Version="X.Y.Z" /> 如果需要)
    [GeneratedRegex("MD5=([a-f0-9]{32})")]
    private static partial Regex GetMd5Regex(); // 声明为 partial 方法
}
