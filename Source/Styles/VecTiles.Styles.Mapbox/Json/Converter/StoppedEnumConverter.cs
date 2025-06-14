using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VecTiles.Styles.Mapbox.Expressions;

namespace VecTiles.Styles.Mapbox.Json.Converter;

public class StoppedEnumConverter<T> : JsonConverter where T: struct, Enum
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(JsonStoppedString) || objectType == typeof(string);
    }

    public override bool CanRead => true;

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);
        if (token.Type == JTokenType.Object)
        {
            var stoppedEnum = new StoppedEnum<T> { Stops = new List<KeyValuePair<float, T>>() };

            JToken? baseToken = token.SelectToken("base");

            if (baseToken != null)
                stoppedEnum.Base = baseToken.ToObject<float>();
            else
                stoppedEnum.Base = 1f;

            JToken? stops = token.SelectToken("stops");

            if (stops == null)
                return stoppedEnum;

            foreach (var stop in stops)
            {
                var zoom = stop?.First?.ToObject<float>() ?? 0.0f;
                var enumString = stop?.Last?.ToObject<string>() ?? string.Empty;
                if (Enum.TryParse<T>(enumString, ignoreCase: true, out var enumValue1))
                {
                    stoppedEnum.Stops.Add(new KeyValuePair<float, T>(zoom, enumValue1));
                }
            }

            return stoppedEnum;
        }

        var result = new StoppedEnum<T> { SingleVal = default(T) };

        if (Enum.TryParse<T>(token?.Value<string>(), ignoreCase: true, out var enumValue2))
        {
            result.SingleVal = enumValue2;
        }

        return result;
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
