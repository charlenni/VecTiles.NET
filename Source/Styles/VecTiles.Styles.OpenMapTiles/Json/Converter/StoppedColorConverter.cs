using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using VecTiles.Common.Extensions;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.OpenMapTiles.Json.Converter;

public class StoppedColorConverter : JsonConverter<StoppedColor>
{
    public override StoppedColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var token = JsonNode.Parse(ref reader);

        if (token is JsonArray)
        {
            // We have an array, so we assume, that it is an expression
            var expression = ExpressionParser.ParseExpression(token);

            if (expression == null)
                throw new JsonException("Couldn't parse expression: " + token!.ToJsonString());

            return new StoppedColor { Expression = expression };
        }

        if (token is JsonObject objectNode)
        {
            var stoppedColor = new StoppedColor { Base = StoppedJson.ReadBase(objectNode) };
            var stops = new List<KeyValuePair<float, Common.Primitives.Color>>();

            foreach (var (zoom, value) in StoppedJson.Stops(objectNode))
            {
                var colorString = StoppedExpression.ToStringValue(MatchExpression.ToValue(value));
                stops.Add(new KeyValuePair<float, Common.Primitives.Color>(zoom, colorString.FromString()));
            }

            stoppedColor.Stops = stops;

            return stoppedColor;
        }

        return new StoppedColor { SingleVal = StoppedExpression.ToColor(MatchExpression.ToValue(token)) };
    }

    public override void Write(Utf8JsonWriter writer, StoppedColor value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Stopped values are only read, never written.");
    }
}
