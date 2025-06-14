using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VecTiles.Styles.Mapbox.Expressions;

namespace VecTiles.Styles.Mapbox.Json.Converter;

public class StoppedStringConverter : JsonConverter
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
            var stoppedString = new StoppedString { Stops = new List<KeyValuePair<float, string>>() };

            JToken? baseToken = token.SelectToken("base");

            if (baseToken != null)
                stoppedString.Base = baseToken.ToObject<float>();
            else
                stoppedString.Base = 1f;

            JToken? stops = token.SelectToken("stops");

            if (stops == null)
                return stoppedString;

            foreach (var stop in stops)
            {
                var zoom = stop?.First?.ToObject<float>() ?? 0.0f;
                var text = stop?.Last?.ToObject<string>() ?? string.Empty;
                stoppedString.Stops.Add(new KeyValuePair<float, string>(zoom, text));
            }

            return stoppedString;
        }

        return new StoppedString() { SingleVal = token?.Value<string>() ?? string.Empty };
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
