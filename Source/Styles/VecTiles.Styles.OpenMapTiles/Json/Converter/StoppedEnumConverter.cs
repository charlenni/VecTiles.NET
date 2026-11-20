using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.OpenMapTiles.Json.Converter;

public class StoppedEnumConverter<T> : JsonConverter<StoppedEnum<T>> where T : struct, Enum
{
    public override StoppedEnum<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var token = JsonNode.Parse(ref reader);

        if (token is JsonArray)
        {
            // We have an array, so we assume, that it is an expression
            var expression = ExpressionParser.ParseExpression(token);

            if (expression == null)
                throw new JsonException("Couldn't parse expression: " + token!.ToJsonString());

            return new StoppedEnum<T> { Expression = expression };
        }

        if (token is JsonObject objectNode)
        {
            var stoppedEnum = new StoppedEnum<T> { Base = StoppedJson.ReadBase(objectNode) };
            var stops = new List<KeyValuePair<float, T>>();

            foreach (var (zoom, value) in StoppedJson.Stops(objectNode))
            {
                var enumString = StoppedExpression.ToStringValue(MatchExpression.ToValue(value));

                if (Enum.TryParse<T>(enumString, ignoreCase: true, out var enumValue))
                {
                    stops.Add(new KeyValuePair<float, T>(zoom, enumValue));
                }
            }

            stoppedEnum.Stops = stops;

            return stoppedEnum;
        }

        var result = new StoppedEnum<T> { SingleVal = default };

        if (Enum.TryParse<T>(StoppedExpression.ToStringValue(MatchExpression.ToValue(token)), ignoreCase: true, out var singleValue))
        {
            result.SingleVal = singleValue;
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, StoppedEnum<T> value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Stopped values are only read, never written.");
    }
}
