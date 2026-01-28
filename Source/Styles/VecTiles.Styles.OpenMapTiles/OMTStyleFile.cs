using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles;

/// <summary>
/// Class for holding OpenMapTiles/OpenMapTiles style file data
/// </summary>
public class OMTStyleFile
{
    [JsonProperty("layers")]
    public OMTLayerStyle[] Layers { get; set; } = [];

    [JsonProperty("sources")]
    public Dictionary<string, OMTSource> Sources { get; set; } = [];

    [JsonProperty("version")]
    public int Version { get; set; }

    [JsonProperty("bearing")]
    public float Bearing { get; set; } = 0.0f;

    [JsonProperty("center")]
    public float[] Center { get; set; } = [];

    [JsonProperty("glyphs")]
    public string Glyphs { get; set; } = "mapbox://fonts/mapbox/{fontstack}/{range}.pbf";

    [JsonProperty("metadata")]
    public JObject? Metadata { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("pitch")]
    public float Pitch { get; set; } = 0.0f;

    [JsonProperty("sprite")]
    internal string spriteFile = string.Empty;

    public SpriteDicionary? Sprites { get; set; }

    [JsonProperty("zoom")]
    public float Zoom { get; set; }
}
