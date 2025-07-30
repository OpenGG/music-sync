using MusicSync.Utils;

namespace MusicSync.Plugins;

public class DrmPlugin(string name, string scriptPath)
{
    public string Name { get; } = name;
    private string ScriptPath { get; } = scriptPath;
    // public string[] Extensions { get; } = extensions;

    public string? Decrypt(string inputFile, TemporaryDirectory tempDir, string[] outputExtensions)
    {
        Directory.CreateDirectory(tempDir.DirectoryPath);
        var psi = new System.Diagnostics.ProcessStartInfo(ScriptPath,
            $"\"{inputFile}\" \"{tempDir.DirectoryPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc == null) return null;
        proc.WaitForExit(120000);
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        if (proc.ExitCode != 0)
        {
            Console.Error.WriteLine($"Plugin error: {stderr.Trim()} {stdout.Trim()}");
            return null;
        }

        var found = Directory.GetFiles(tempDir.DirectoryPath, "*", SearchOption.AllDirectories)
            .FirstOrDefault(f => outputExtensions.Contains(Path.GetExtension(f).ToLower()));
        return found;
    }
}
