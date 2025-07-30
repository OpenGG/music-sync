using System.Linq;

namespace MusicSync.Plugins
{
    public class DrmPlugin
    {
        public DrmPlugin(string name, string scriptPath, string[] extensions)
        {
            Name = name;
            ScriptPath = scriptPath;
            Extensions = extensions;
        }

        public string Name { get; }
        public string ScriptPath { get; }
        public string[] Extensions { get; }

        public string? Decrypt(string inputFile, string[] outputExtensions)
        {
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
            System.IO.Directory.CreateDirectory(tempDir);
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
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(120000);
                if (proc.ExitCode != 0)
                {
                    System.Console.WriteLine($"Plugin error: {stderr.Trim()} {stdout.Trim()}");
                    return null;
                }
                var found = System.IO.Directory.GetFiles(tempDir, "*", System.IO.SearchOption.AllDirectories)
                    .FirstOrDefault(f => outputExtensions.Contains(System.IO.Path.GetExtension(f).ToLower()));
                if (found == null) return null;
                string dest = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName() + System.IO.Path.GetExtension(found));
                System.IO.File.Move(found, dest, true);
                return dest;
            }
            finally
            {
                try { if (System.IO.Directory.Exists(tempDir)) System.IO.Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
