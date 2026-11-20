using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.OpenMapTiles.Json.Converter;

public class StoppedStringConverter : JsonConverter<StoppedString>
{
    public override StoppedString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var token = JsonNode.Parse(ref reader);

        if (token is JsonArray)
        {
            // We have an array, so we assume, that it is an expression
            var expression = ExpressionParser.ParseExpression(token);

            if (expression == null)
                throw new JsonException("Couldn't parse expression: " + token!.ToJsonString());

            return new StoppedString { Expression = expression };
        }

        if (token is JsonObject objectNode)
        {
            var stoppedString = new StoppedString { Base = StoppedJson.ReadBase(objectNode) };
            var stops = new List<KeyValuePair<float, string>>();

            foreach (var (zoom, value) in StoppedJson.Stops(objectNode))
            {
                stops.Add(new KeyValuePair<float, string>(zoom, StoppedExpression.ToStringValue(MatchExpression.ToValue(value))));
            }

            stoppedString.Stops = stops;

            return stoppedString;
        }

        return new StoppedString { SingleVal = StoppedExpression.ToStringValue(MatchExpression.ToValue(token)) };
    }

    public override void Write(Utf8JsonWriter writer, StoppedString value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Stopped values are only read, never written.");
    }
}
