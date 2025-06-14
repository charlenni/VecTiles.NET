using Newtonsoft.Json;

namespace VecTiles.Styles.Mapbox.Json.Converter;

public class EnumConverter<T> : JsonConverter where T : struct, Enum
{
    public override bool CanConvert(Type objectType) => objectType == typeof(T);

    public override bool CanRead => true;

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            var value = reader.Value?.ToString() ?? string.Empty;
            value = value.Replace("-", "");
            if (Enum.TryParse<T>(value, ignoreCase: true, out var result))
                return result;
        }

        return default(T); // oder throw new JsonSerializationException(...)
    }

    public override bool CanWrite => true;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        writer.WriteValue(value?.ToString());
    }
}
