using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VecTiles.Common.Extensions;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapbox.Expressions;

namespace VecTiles.Styles.Mapbox.Json.Converter;

public class StoppedColorConverter : JsonConverter
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
            var stoppedColor = new StoppedColor { Stops = new List<KeyValuePair<float, Color>>() };

            JToken? baseToken = token.SelectToken("base");

            if (baseToken != null)
                stoppedColor.Base = baseToken.ToObject<float>();
            else
                stoppedColor.Base = 1f;

            JToken? stops = token.SelectToken("stops");

            if (stops == null)
                return stoppedColor;

            foreach (var stop in stops)
            {
                var zoom = stop?.First?.ToObject<float>() ?? 0.0f;
                var colorString = stop?.Last?.ToObject<string>() ?? string.Empty;
                stoppedColor.Stops.Add(new KeyValuePair<float, Color>(zoom, colorString.FromString()));
            }

            return stoppedColor;
        }

        return new StoppedColor() { SingleVal = token?.Value<string>()?.FromString() ?? Color.Black };
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
