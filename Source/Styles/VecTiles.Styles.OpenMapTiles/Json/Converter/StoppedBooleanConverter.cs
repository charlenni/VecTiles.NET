using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.OpenMapTiles.Json.Converter;

public class StoppedBooleanConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(JsonStoppedBoolean) || objectType == typeof(bool);
    }

    public override bool CanRead => true;

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);

        if (token.Type == JTokenType.Array)
        {
            // We have an array, so we assume, that it is an expression
            var expression = ExpressionParser.ParseExpression(token, typeof(bool));

            if (expression == null)
                throw new ArgumentException("Couldn't parse expression: " + token.ToString(Formatting.None));

            return new StoppedBoolean { Expression = expression };
        }

        if (token.Type == JTokenType.Object)
        {
            var stoppedBoolean = new StoppedBoolean { Stops = new List<KeyValuePair<float, bool>>() };

            JToken? stops = token.SelectToken("stops");

            if (stops == null)
                return stoppedBoolean;

            foreach (JToken? stop in stops)
            {
                var zoom = stop?.First?.ToObject<float>() ?? 0.0f;
                var value = stop?.Last?.ToObject<bool>() ?? true;
                stoppedBoolean.Stops.Add(new KeyValuePair<float, bool>((float)zoom, value));
            }

            return stoppedBoolean;
        }

        return new StoppedBoolean() { SingleVal = token.Value<bool>() };
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
