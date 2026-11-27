using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapLibre.Json.Converter;

/// <summary>
/// JSON converter for zoom-dependent boolean values. Accepts a plain boolean,
/// a stops object, or an expression array.
/// </summary>
public class StoppedBooleanConverter : JsonConverter<StoppedBoolean>
{
    /// <inheritdoc />
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

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always thrown; stopped values are read-only.</exception>
    public override void Write(Utf8JsonWriter writer, StoppedBoolean value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Stopped values are only read, never written.");
    }
}
