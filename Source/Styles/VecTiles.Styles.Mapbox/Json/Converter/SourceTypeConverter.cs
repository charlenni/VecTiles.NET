using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using VecTiles.Common.Enums;

namespace VecTiles.Styles.Mapbox.Json.Converter;

public class SourceTypeConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(string);
    }

    public override bool CanRead => true;

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);
        
        if (Enum.TryParse(token.ToString(), out SourceType sourceType))
            return sourceType;

        throw new InvalidEnumArgumentException($"Source type '{token.ToString()}' isn't valid");
    }

    public override bool CanWrite => true;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
