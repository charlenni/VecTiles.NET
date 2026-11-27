using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using VecTiles.Common.Primitives;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapLibre.Json.Converter;

/// <summary>
/// JSON converter for string array properties that may also be given as an
/// expression array evaluating to such an array (e.g. "text-font" or
/// "text-variable-anchor"). The expression is evaluated once with an empty
/// feature, because these properties are resolved when the layer style is
/// created; a failing evaluation falls back to an empty array, so the default
/// handling of the property applies.
/// </summary>
public class StringArrayConverter : JsonConverter<string[]>
{
    /// <inheritdoc />
    public override string[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return [];

        var token = JsonNode.Parse(ref reader);

        if (token is not JsonArray array)
            return [];

        // A plain value list is an array of strings that is not itself an
        // expression; expression arrays always start with a known operator
        var expression = ExpressionParser.ParseExpression(array.DeepClone());

        if (expression == null)
            return array.Select(node => node?.GetValue<string>() ?? string.Empty).ToArray();

        if (expression.Evaluate(EvaluationContext.Empty) is object?[] values)
            return values.Select(value => StoppedExpression.ToStringValue(value)).ToArray();

        return [];
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always thrown; styles are read-only.</exception>
    public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Styles are only read, never written.");
    }
}
