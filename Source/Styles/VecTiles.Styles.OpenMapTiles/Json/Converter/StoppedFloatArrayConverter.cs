using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.OpenMapTiles.Json.Converter;

public class StoppedFloatArrayConverter : JsonConverter<StoppedFloatArray>
{
    public override StoppedFloatArray Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var token = JsonNode.Parse(ref reader);

        if (token is JsonArray)
        {
            // An array is either an expression or a plain float array
            // (e.g. line-dasharray or text-offset), so we try an expression
            // first and fall back to a literal array
            var expression = ExpressionParser.ParseExpression(token, typeof(float[]));

            if (expression != null)
            {
                return new StoppedFloatArray { Expression = expression };
            }
        }

        if (token is JsonObject objectNode)
        {
            var stoppedFloat = new StoppedFloatArray { Base = StoppedJson.ReadBase(objectNode) };
            var stops = new List<KeyValuePair<float, float[]>>();

            foreach (var (zoom, value) in StoppedJson.Stops(objectNode))
            {
                stops.Add(new KeyValuePair<float, float[]>(zoom, StoppedExpression.ToFloatArray(MatchExpression.ToValue(value))));
            }

            stoppedFloat.Stops = stops;

            return stoppedFloat;
        }

        return new StoppedFloatArray { SingleVal = StoppedExpression.ToFloatArray(MatchExpression.ToValue(token)) };
    }

    public override void Write(Utf8JsonWriter writer, StoppedFloatArray value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Stopped values are only read, never written.");
    }
}
