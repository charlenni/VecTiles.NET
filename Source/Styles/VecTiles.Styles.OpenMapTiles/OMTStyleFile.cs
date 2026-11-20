using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles;

/// <summary>
/// Class for holding OpenMapTiles/OpenMapTiles style file data
/// </summary>
public class OMTStyleFile
{
    [JsonPropertyName("layers")]
    public OMTLayerStyle[] Layers { get; set; } = [];

    [JsonPropertyName("sources")]
    public Dictionary<string, OMTSource> Sources { get; set; } = [];

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("bearing")]
    public float Bearing { get; set; } = 0.0f;

    [JsonPropertyName("center")]
    public float[] Center { get; set; } = [];

    [JsonPropertyName("glyphs")]
    public string Glyphs { get; set; } = "mapbox://fonts/mapbox/{fontstack}/{range}.pbf";

    [JsonPropertyName("metadata")]
    public JsonObject? Metadata { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("pitch")]
    public float Pitch { get; set; } = 0.0f;

    [JsonPropertyName("sprite")]
    [JsonInclude]
    internal string spriteFile = string.Empty;

    public SpriteDictionary? Sprites { get; set; }

    [JsonPropertyName("zoom")]
    public float? Zoom { get; set; }
}
