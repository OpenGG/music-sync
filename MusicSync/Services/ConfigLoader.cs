using System.Diagnostics.CodeAnalysis;
using MusicSync.Models;
using MusicSync.Utils;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MusicSync.Services;

[ExcludeFromCodeCoverage]
public static class ConfigLoader
{
    public static Config Load(string? configPath)
    {
        var possible = new[]
        {
            configPath,
            Path.Join(Directory.GetCurrentDirectory(), "config.yaml"),
            Path.Join(AppContext.BaseDirectory, "config.yaml")
        };

        foreach (var p in possible)
        {
            if (string.IsNullOrEmpty(p))
            {
                continue;
            }

            if (!File.Exists(p))
            {
                continue;
            }

            Console.WriteLine($"Loading configuration from: {p}");
            var deserializer = new StaticDeserializerBuilder(new ConfigYamlContext())
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            using StreamReader reader = new(p);
            return deserializer.Deserialize<Config>(reader);
        }

        Console.WriteLine("Error: config.yaml not found.");
        var paths = string.Join(", ", possible);
        Console.WriteLine("Tried: " + paths);
        throw new Exception($"Config.yaml not found in {paths}");
        // Environment.Exit(1);
        // return new Config();
    }
}
