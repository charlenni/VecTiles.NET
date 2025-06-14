using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VecTiles.Styles.Mapbox.Expressions;

namespace VecTiles.Styles.Mapbox.Json.Converter;

public class StoppedFloatArrayConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(JsonStoppedFloat) || objectType == typeof(int);
    }

    public override bool CanRead => true;

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);
        if (token.Type == JTokenType.Object)
        {
            var stoppedFloat = new StoppedFloatArray { Stops = new List<KeyValuePair<float, float[]>>() };

            JToken? baseToken = token.SelectToken("base");

            if (baseToken != null)
                stoppedFloat.Base = baseToken.ToObject<float>();
            else
                stoppedFloat.Base = 1f;

            JToken? stops = token.SelectToken("stops");

            if (stops == null)
                return stoppedFloat;

            foreach (var stop in stops)
            {
                var zoom = stop?.First?.ToObject<float>() ?? 0.0f;
                var value = stop?.Last?.ToObject<float[]>() ?? [];
                stoppedFloat.Stops.Add(new KeyValuePair<float, float[]>(zoom, value));
            }

            return stoppedFloat;
        }

        return new StoppedFloatArray() { SingleVal = token.ToObject<float[]>() ?? [] };
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
