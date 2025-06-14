using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VecTiles.Styles.Mapbox.Expressions;

namespace VecTiles.Styles.Mapbox.Json.Converter;

public class ExpressionConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        //  objectType == typeof(JsonStoppedString) || objectType == typeof(string);
        return true;
    }

    public override bool CanRead => true;

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);

        switch (token.Type)
        {
            case JTokenType.Object:
                // It is a object, so we assume, that it is a stopped type
                if (objectType.GenericTypeArguments.ToString() == "string")
                    return CreateStoppedString(token);
                break;
            case JTokenType.Array:
                // We have an array, so we assume, that it is an expresion
                break;
        }

        return null;
    }

    public StoppedString CreateStoppedString(JToken token)
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

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
