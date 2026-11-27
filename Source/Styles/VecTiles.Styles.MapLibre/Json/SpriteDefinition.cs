using System.Text.Json.Serialization;
using VecTiles.Styles.MapLibre.Json.Converter;

namespace VecTiles.Styles.MapLibre.Json;

/// <summary>
/// A sprite definition of a style file. Mapbox style files reference a single
/// sprite by its base URL, MapLibre style files can reference several sprites
/// as id/URL pairs; their sprites are then referenced in icon-image and pattern
/// properties as "id:name".
/// </summary>
public class SpriteDefinition
{
    /// <summary>
    /// Id the sprites of this definition are prefixed with in icon-image and
    /// pattern properties ("id:name"), or null for an unprefixed single sprite.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Base URL of the sprite atlas files, without .png/.json or @2x extension.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Sprite definitions of a style file: either a single sprite URL (Mapbox
/// style spec) or a list of id/URL pairs (MapLibre style spec).
/// </summary>
[JsonConverter(typeof(OMTSpriteDefinitionCollectionConverter))]
public class OMTSpriteDefinitionCollection : List<SpriteDefinition>
{
}
