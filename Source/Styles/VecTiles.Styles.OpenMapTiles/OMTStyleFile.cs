using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles.Json;

namespace VecTiles.Styles.OpenMapTiles;

/// <summary>
/// Class for holding OpenMapTiles/OpenMapTiles style file data
/// </summary>
public class OMTStyleFile
{
    /// <summary>
    /// Layers of the style, in the order they are drawn.
    /// </summary>
    [JsonPropertyName("layers")]
    public OMTLayerStyle[] Layers { get; set; } = [];

    /// <summary>
    /// Source definitions of the style, keyed by the name referenced by the layers.
    /// </summary>
    [JsonPropertyName("sources")]
    public Dictionary<string, OMTSource> Sources { get; set; } = [];

    /// <summary>
    /// Version of the style specification the file conforms to.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; }

    /// <summary>
    /// Default bearing (rotation) of the map, in degrees.
    /// </summary>
    [JsonPropertyName("bearing")]
    public float Bearing { get; set; } = 0.0f;

    /// <summary>
    /// Default center of the map as [longitude, latitude].
    /// </summary>
    [JsonPropertyName("center")]
    public float[] Center { get; set; } = [];

    /// <summary>
    /// Sign-signed font (SDF) glyph template URL, containing "{fontstack}" and "{range}".
    /// </summary>
    [JsonPropertyName("glyphs")]
    public string Glyphs { get; set; } = "mapbox://fonts/mapbox/{fontstack}/{range}.pbf";

    /// <summary>
    /// Custom metadata of the style, as present in the style file.
    /// </summary>
    [JsonPropertyName("metadata")]
    public JsonObject? Metadata { get; set; }

    /// <summary>
    /// Name of the style.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Default pitch (tilt) of the map, in degrees.
    /// </summary>
    [JsonPropertyName("pitch")]
    public float Pitch { get; set; } = 0.0f;

    /// <summary>
    /// Sprite definitions of the style: either a single sprite URL (Mapbox
    /// style spec) or a list of id/URL pairs (MapLibre style spec), where
    /// sprites are referenced as "id:name" in icon-image and pattern properties.
    /// </summary>
    [JsonPropertyName("sprite")]
    public OMTSpriteDefinitionCollection SpriteDefinitions { get; set; } = [];

    /// <summary>
    /// Sprites loaded from the style's sprite file image and atlas definition,
    /// or null until the sprites have been loaded.
    /// </summary>
    public SpriteDictionary? Sprites { get; set; }

    /// <summary>
    /// Default zoom level of the map.
    /// </summary>
    [JsonPropertyName("zoom")]
    public float? Zoom { get; set; }
}
