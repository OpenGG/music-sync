using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using MusicSync.Models;

using System.Diagnostics.CodeAnalysis;

namespace MusicSync.Services
{
    [ExcludeFromCodeCoverage]
    public static class ConfigLoader
    {
        public static Config Load(string? configPath)
        {
            var possible = new[]
            {
                configPath,
                Path.Combine(Directory.GetCurrentDirectory(), "config.yaml"),
                Path.Combine(AppContext.BaseDirectory, "config.yaml")
            };

            foreach (var p in possible)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (File.Exists(p))
                {
                    Console.WriteLine($"Loading configuration from: {p}");
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(UnderscoredNamingConvention.Instance)
                        .Build();
                    using var reader = new StreamReader(p);
                    return deserializer.Deserialize<Config>(reader);
                }
            }
            Console.WriteLine("Error: config.yaml not found.");
            Console.WriteLine("Tried: " + string.Join(", ", possible));
            Environment.Exit(1);
            return new Config();
        }
    }
}
