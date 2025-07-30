namespace MusicSync.Plugins;

public class DrmPlugin(string name, string scriptPath)
{
    public string Name { get; } = name;
    private string ScriptPath { get; } = scriptPath;
    // public string[] Extensions { get; } = extensions;

    public string? Decrypt(string inputFile, string[] outputExtensions)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(ScriptPath, $"\"{inputFile}\" \"{tempDir}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return null;
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(120000);
            if (proc.ExitCode != 0)
            {
                Console.WriteLine($"Plugin error: {stderr.Trim()} {stdout.Trim()}");
                return null;
            }
            var found = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories)
                .FirstOrDefault(f => outputExtensions.Contains(Path.GetExtension(f).ToLower()));
            if (found == null) return null;
            var dest = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + Path.GetExtension(found));
            File.Move(found, dest, true);
            return dest;
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            catch
            {
                // ignored
            }
        }
    }
}
