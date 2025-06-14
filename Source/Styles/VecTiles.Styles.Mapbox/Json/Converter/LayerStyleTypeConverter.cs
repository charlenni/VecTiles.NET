using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VecTiles.Common.Enums;

namespace VecTiles.Styles.Mapbox.Json.Converter;

/// <summary>
/// JSON converter for mapping Mapbox GL style layer type strings to <see cref="LayerStyleType"/> enum values.
/// </summary>
public class LayerStyleTypeConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(string);

    public override bool CanRead => true;

    public override bool CanWrite => false;

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var value = (reader.Value ?? JToken.Load(reader).ToString()).ToString()?.ToLowerInvariant();
        return value switch
        {
            "background" => LayerStyleType.Background,
            "fill" => LayerStyleType.Fill,
            "line" => LayerStyleType.Line,
            "raster" => LayerStyleType.Raster,
            "symbol" => LayerStyleType.Symbol,
            "fill-extrusion" => LayerStyleType.FillExtrusion,
            _ => throw new NotImplementedException($"LayerStyle with type '{value}' is unknown")
        };
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) =>
        throw new NotImplementedException();
}
