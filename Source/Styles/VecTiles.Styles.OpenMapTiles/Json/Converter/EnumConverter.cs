using System.Text.Json;
using System.Text.Json.Serialization;
using VecTiles.Common.Enums;

namespace VecTiles.Styles.OpenMapTiles.Json.Converter;

public class EnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString() ?? string.Empty;
            value = value.Replace("-", "");
            if (Enum.TryParse<T>(value, ignoreCase: true, out var result))
                return result;

            // Unsupported layer types must not fall back to the first enum
            // member (Background), which would paint the whole tile.
            if (typeof(T) == typeof(LayerStyleType))
                return (T)(object)LayerStyleType.Unknown;
        }

        return default;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
