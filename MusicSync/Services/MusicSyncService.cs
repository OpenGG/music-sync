namespace MusicSync.Services;

public class MusicSyncService(MusicFileProcessor processor, IEnumerable<string> sourceDirs)
{
    public void Run()
    {
        foreach (var dir in sourceDirs)
        {
            if (!Directory.Exists(dir))
            {
                Console.WriteLine($"Warning: Source directory not found: {dir}. Skipping.");
                continue;
            }
            Console.WriteLine($"\n--- Processing files from: {dir} ---");
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                processor.ProcessFile(file, dir);
            }
        }
        Console.WriteLine("\n--- Music synchronization complete ---");
    }
}
