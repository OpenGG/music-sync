namespace MusicSync.Utils;

using System.Text.Json.Serialization;
using Models;

// C# 11 introduces a new way to use System.Text.Json with source generation for native AOT compilation.
// This new partial class allows the compiler to automatically generate the necessary serialization and deserialization code.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, WriteIndented = true)]
[JsonSerializable(typeof(Config))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<DrmPluginConfig>))]
public partial class ConfigJsonContext : JsonSerializerContext;

