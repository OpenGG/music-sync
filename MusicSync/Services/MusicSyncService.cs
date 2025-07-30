using System;
using System.Collections.Generic;
using System.IO;
using MusicSync.Models;

namespace MusicSync.Services
{
    public class MusicSyncService
    {
        private readonly MusicFileProcessor _processor;
        private readonly IEnumerable<string> _sourceDirs;

        public MusicSyncService(MusicFileProcessor processor, IEnumerable<string> sourceDirs)
        {
            _processor = processor;
            _sourceDirs = sourceDirs;
        }

        public void Run()
        {
            foreach (var dir in _sourceDirs)
            {
                if (!Directory.Exists(dir))
                {
                    Console.WriteLine($"Warning: Source directory not found: {dir}. Skipping.");
                    continue;
                }
                Console.WriteLine($"\n--- Processing files from: {dir} ---");
                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    _processor.ProcessFile(file, dir);
                }
            }
            Console.WriteLine("\n--- Music synchronization complete ---");
        }
    }
}
