using System.Text.Json;
using System.Text.Json.Nodes; // Required for JsonNode and JsonObject
using MusicSync.Models;
using MusicSync.Utils;

namespace MusicSync.Services;

/// <summary>
/// Handles loading and validating the configuration file.
/// </summary>
public static class ConfigLoader
{
    /// <summary>
    /// Loads and validates the configuration from a JSON file.
    /// </summary>
    /// <param name="configPath">Optional path to the config file.</param>
    /// <returns>A validated Config object.</returns>
    /// <exception cref="FileNotFoundException">Thrown if config.json cannot be found.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the config file fails schema validation.</exception>
    /// <exception cref="JsonException">Thrown if the config file is malformed JSON.</exception>
    public static Config Load(string? configPath)
    {
        // Define possible file paths for the config file.
        var possible = new[]
        {
            configPath, Path.Join(Directory.GetCurrentDirectory(), "config.json"),
            Path.Join(AppContext.BaseDirectory, "config.json")
        };

        foreach (var p in possible)
        {
            if (string.IsNullOrEmpty(p) || !File.Exists(p))
            {
                continue;
            }

            Console.WriteLine($"Loading configuration from: {p}");
            var configJson = File.ReadAllText(p);

            // --- Step 1: Perform manual, reflection-free validation ---
            try
            {
                var jsonObject = JsonNode.Parse(configJson)?.AsObject();
                if (jsonObject == null)
                {
                    throw new InvalidOperationException("Configuration file is not a valid JSON object.");
                }

                ValidateConfigProperties(jsonObject);
            }
            catch (JsonException ex)
            {
                // This catches malformed JSON before we even try to deserialize.
                throw new InvalidOperationException($"Configuration file is not valid JSON: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                // This catches our custom validation errors.
                throw new InvalidOperationException($"Configuration file validation failed: {ex.Message}");
            }

            // --- Step 2: If valid, deserialize the JSON to the C# object using source generation ---
            return JsonSerializer.Deserialize(configJson, ConfigJsonContext.Default.Config)
                   ?? throw new JsonException("Failed to deserialize configuration file.");
        }

        var paths = string.Join(", ", possible.Where(s => !string.IsNullOrEmpty(s)));
        Console.WriteLine("Error: config.json not found.");
        Console.WriteLine("Tried: " + paths);
        throw new FileNotFoundException($"Config.json not found in any of the following locations: {paths}");
    }

    /// <summary>
    /// Performs manual, reflection-free validation of the JSON structure.
    /// This version is more declarative and less verbose.
    /// </summary>
    private static void ValidateConfigProperties(JsonObject jsonObject)
    {
        // Check for required properties
        var requiredProperties = new[] { "music_sources", "music_dest_dir", "drm_plugins", "music_extensions" };
        foreach (var prop in requiredProperties)
        {
            if (!jsonObject.ContainsKey(prop))
            {
                throw new InvalidOperationException($"Missing required property: '{prop}'");
            }
        }

        // Validate individual property types
        ValidateArrayOfStrings(jsonObject, "music_sources", "'music_sources' must be an array of strings.");
        ValidateStringProperty(jsonObject, "music_dest_dir", "'music_dest_dir' must be a string.");
        ValidateArrayOfStrings(jsonObject, "music_extensions", "'music_extensions' must be an array of strings.");

        // Validate the more complex 'drm_plugins' array
        if (jsonObject["drm_plugins"] is not JsonArray drmPlugins)
        {
            throw new InvalidOperationException("'drm_plugins' must be an array of objects.");
        }

        foreach (var plugin in drmPlugins)
        {
            if (plugin is not JsonObject pluginObject)
            {
                throw new InvalidOperationException("Each item in 'drm_plugins' must be a JSON object.");
            }

            ValidateStringProperty(pluginObject, "name",
                "Plugin in 'drm_plugins' must have a 'name' that is a string.");
            ValidateBooleanProperty(pluginObject, "enabled",
                "Plugin in 'drm_plugins' must have an 'enabled' property that is a boolean.");
            ValidateArrayOfStrings(pluginObject, "extensions",
                "Plugin in 'drm_plugins' must have an 'extensions' array of strings.");
        }
    }

    // --- Helper methods to reduce boilerplate validation code ---
    private static void ValidateStringProperty(JsonObject jsonObject, string propertyName, string errorMessage)
    {
        if (jsonObject[propertyName] is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private static void ValidateBooleanProperty(JsonObject jsonObject, string propertyName, string errorMessage)
    {
        if (jsonObject[propertyName] is not JsonValue value ||
            value.GetValueKind() is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private static void ValidateArrayOfStrings(JsonObject jsonObject, string propertyName, string errorMessage)
    {
        if (jsonObject[propertyName] is not JsonArray jsonArray ||
            jsonArray.Any(n => n is not JsonValue || n.GetValueKind() != JsonValueKind.String))
        {
            throw new InvalidOperationException(errorMessage);
        }
    }
}
