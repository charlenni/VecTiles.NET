using System.Text.Json;
using System.Text.Json.Serialization;
using VecTiles.Styles.MapLibre.Json;

namespace VecTiles.Styles.MapLibre.Json.Converter;

/// <summary>
/// Json converter for the sprite property of a style file, which is either a
/// single sprite URL (Mapbox style spec) or a list of id/URL pairs (MapLibre
/// style spec).
/// </summary>
public class OMTSpriteDefinitionCollectionConverter : JsonConverter<OMTSpriteDefinitionCollection>
{
    /// <inheritdoc />
    public override OMTSpriteDefinitionCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new OMTSpriteDefinitionCollection
            {
                new SpriteDefinition { Url = reader.GetString() ?? string.Empty }
            };
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var definitions = new OMTSpriteDefinitionCollection();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    definitions.Add(new SpriteDefinition { Url = reader.GetString() ?? string.Empty });
                    continue;
                }

                var definition = JsonSerializer.Deserialize<SpriteDefinition>(ref reader, options);
                if (definition != null)
                    definitions.Add(definition);
            }

            return definitions;
        }

        throw new JsonException($"Cannot convert token type '{reader.TokenType}' to sprite definitions.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, OMTSpriteDefinitionCollection value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var definition in value)
        {
            if (string.IsNullOrEmpty(definition.Id))
                writer.WriteStringValue(definition.Url);
            else
                JsonSerializer.Serialize(writer, definition, options);
        }

        writer.WriteEndArray();
    }
}
