using MusicSync.Models;
using MusicSync.Plugins;
using System.Diagnostics.CodeAnalysis;

namespace MusicSync.Services;

[ExcludeFromCodeCoverage]
public class DrmPluginLoader
{
    private readonly Dictionary<string, DrmPluginConfig> _extToConfig = new();
    private readonly Dictionary<string, DrmPlugin?> _loaded = new();

    public DrmPluginLoader(IEnumerable<DrmPluginConfig> configs)
    {
        foreach (var cfg in configs)
        {
            if (!cfg.Enabled || string.IsNullOrEmpty(cfg.Name))
                continue;
            foreach (var ext in cfg.Extensions)
                _extToConfig[ext.ToLower()] = cfg;
        }
    }

    public DrmPlugin? Resolve(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLower();
        if (!_extToConfig.TryGetValue(ext, out var cfg)) return null;
        if (_loaded.TryGetValue(cfg.Name, out var plugin)) return plugin;
        plugin = Load(cfg);
        _loaded[cfg.Name] = plugin;
        return plugin;
    }

    private static DrmPlugin? Load(DrmPluginConfig cfg)
    {
        var cwd = Directory.GetCurrentDirectory();
        var pluginDir = Path.Combine(AppContext.BaseDirectory, "drm_plugins");
        var candidates = new[] { cfg.Name, cfg.Name + ".sh", cfg.Name + ".bash" };
        foreach (var candidate in candidates)
        {
            string path;
            if (Path.IsPathRooted(candidate))
            {
                path = candidate;
                if (File.Exists(path)) return Build(cfg, path);
                continue;
            }

            path = Path.Combine(cwd, candidate);
            if (File.Exists(path)) return Build(cfg, path);
            path = Path.Combine(pluginDir, candidate);
            if (File.Exists(path)) return Build(cfg, path);
        }

        Console.WriteLine($"Warning: DRM plugin script for '{cfg.Name}' not found. Skipping.");
        return null;
    }

    private static DrmPlugin Build(DrmPluginConfig cfg, string path)
    {
        Console.WriteLine($"Loaded DRM plugin: {cfg.Name} (script: {Path.GetFileName(path)})");
        return new DrmPlugin(cfg.Name, path);
        // return new DrmPlugin(cfg.name, path, cfg.extensions.ToArray());
    }
}