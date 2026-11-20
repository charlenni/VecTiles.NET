using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.OpenMapTiles.Json.Converter;

public class StoppedBooleanConverter : JsonConverter<StoppedBoolean>
{
    public override StoppedBoolean Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var token = JsonNode.Parse(ref reader);

        if (token is JsonArray)
        {
            // We have an array, so we assume, that it is an expression
            var expression = ExpressionParser.ParseExpression(token, typeof(bool));

            if (expression == null)
                throw new JsonException("Couldn't parse expression: " + token!.ToJsonString());

            return new StoppedBoolean { Expression = expression };
        }

        if (token is JsonObject objectNode)
        {
            var stoppedBoolean = new StoppedBoolean();
            var stops = new List<KeyValuePair<float, bool>>();

            foreach (var (zoom, value) in StoppedJson.Stops(objectNode))
            {
                // A missing stop value counts as true
                stops.Add(new KeyValuePair<float, bool>(zoom, value == null || StoppedExpression.ToBoolean(MatchExpression.ToValue(value))));
            }

            stoppedBoolean.Stops = stops;

            return stoppedBoolean;
        }

        return new StoppedBoolean { SingleVal = StoppedExpression.ToBoolean(MatchExpression.ToValue(token)) };
    }

    public override void Write(Utf8JsonWriter writer, StoppedBoolean value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Stopped values are only read, never written.");
    }
}
