using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapLibre.Json.Converter;

/// <summary>
/// JSON converter for zoom-dependent float values. Accepts a plain number,
/// a stops object, or an expression array.
/// </summary>
public class StoppedFloatConverter : JsonConverter<StoppedFloat>
{
    /// <inheritdoc />
    public override StoppedFloat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var token = JsonNode.Parse(ref reader);

        if (token is JsonArray)
        {
            // We have an array, so we assume, that it is an expression
            var expression = ExpressionParser.ParseExpression(token);

            if (expression == null)
                throw new JsonException("Couldn't parse expression: " + token!.ToJsonString());

            return new StoppedFloat { Expression = expression };
        }

        if (token is JsonObject objectNode)
        {
            var stoppedFloat = new StoppedFloat { Base = StoppedJson.ReadBase(objectNode) };
            var stops = new List<KeyValuePair<float, float>>();

            foreach (var (zoom, value) in StoppedJson.Stops(objectNode))
            {
                stops.Add(new KeyValuePair<float, float>(zoom, StoppedExpression.ToFloat(MatchExpression.ToValue(value))));
            }

            stoppedFloat.Stops = stops;

            return stoppedFloat;
        }

        return new StoppedFloat { SingleVal = StoppedExpression.ToFloat(MatchExpression.ToValue(token)) };
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always thrown; stopped values are read-only.</exception>
    public override void Write(Utf8JsonWriter writer, StoppedFloat value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Stopped values are only read, never written.");
    }
}
