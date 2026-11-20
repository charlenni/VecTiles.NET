using System.Text.Json;
using System.Text.Json.Serialization;

namespace VecTiles.Styles.OpenMapTiles.Json;

/// <summary>
/// Json options for reading OpenMapTiles/Mapbox style files. Real style files
/// may contain comments, trailing commas and numbers written as strings, so
/// reading is tolerant there. Property matching is case-insensitive.
/// </summary>
public static class StyleJson
{
    /// <summary>
    /// Shared options for all style file deserialization
    /// </summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        return options;
    }
}
