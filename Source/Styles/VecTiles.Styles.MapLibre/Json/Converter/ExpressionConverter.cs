using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using VecTiles.Common.Interfaces;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapLibre.Json.Converter;

/// <summary>
/// Json converter for layer filters. A layer filter is either an expression
/// array (including the legacy v7 filter syntax) or null.
/// </summary>
public class ExpressionConverter : JsonConverter<IExpression>
{
    /// <inheritdoc />
    public override IExpression Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // A missing/undefined filter is valid for all features
        if (reader.TokenType == JsonTokenType.Null)
            return Expression.Empty;

        var token = JsonNode.Parse(ref reader);

        if (token is JsonArray array)
        {
            // We have an array, so we assume, that it is an expression
            return ConvertFilter(array);
        }

        return null;
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always thrown; expressions are read-only.</exception>
    public override void Write(Utf8JsonWriter writer, IExpression value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Expressions are only read, never written.");
    }

    /// <summary>
    /// Convert a layer filter to an expression. Filters in expression syntax are
    /// parsed directly, legacy filter arrays are converted to expression syntax first.
    /// </summary>
    /// <param name="filter">Array with the layer filter</param>
    /// <returns>An expression that evaluates to a boolean for every feature</returns>
    public static IExpression ConvertFilter(JsonArray? filter)
    {
        // An empty filter is valid for all features
        if (filter == null || filter.Count == 0)
            return Expression.Empty;

        var token = IsExpressionFilter(filter) ? filter : ConvertLegacyFilter(filter);
        var expression = ExpressionParser.ParseExpression(token, typeof(bool));

        if (expression == null)
            throw new ArgumentException(nameof(filter), new FormatException("Couldn't parse filter: " + token.ToJsonString()));

        return expression;
    }

    /// <summary>
    /// Determines whether the given filter array is already in expression syntax
    /// (rather than the legacy v7 filter syntax).
    /// </summary>
    /// <param name="filter">The filter array to check; may be null or empty.</param>
    /// <returns>true if the filter is an expression array; otherwise false.</returns>
    public static bool IsExpressionFilter(JsonArray? filter)
    {
        if (filter == null || filter.Count == 0)
            return false;

        if (filter[0] is not JsonValue operatorValue || !operatorValue.TryGetValue<string>(out var op))
            return false;

        switch (op)
        {
            case "has":
                if (filter.Count < 2)
                    return false;
                var operand = MatchExpression.ToValue(filter[1]) as string ?? string.Empty;
                return operand != "$id" && operand != "$type";
            case "in":
            case "!in":
                // Legacy form is ["in", key, value1, value2, ...] with a plain
                // string key and scalar values; any array operand means the
                // filter already uses expression syntax
                for (int i = 1; i < filter.Count; i++)
                {
                    if (filter[i] is JsonArray)
                        return true;
                }

                return false;
            case "!has":
            case "none":
                // Pure legacy operators; expressions use ["!", ["has", ...]]
                return false;
            case "==":
            case "!=":
            case ">":
            case ">=":
            case "<":
            case "<=":
                return filter.Count == 3 && (filter[1] is JsonArray || filter[2] is JsonArray);
            case "any":
            case "all":
                // If we only have an "all" as filter
                if (filter.Count == 1)
                    return false;
                for (int i = 1; i < filter.Count; i++)
                {
                    if (!IsExpressionFilter(filter[i] as JsonArray) && !(filter[i] is JsonValue value && value.TryGetValue(out bool _)))
                        return false;
                }

                return true;
        }

        return true;
    }

    /// <summary>
    /// Convert a legacy (v7) filter array to the matching expression array, e.g.
    /// ["==", "$type", "Polygon"] to ["==", ["geometry-type"], "Polygon"] and
    /// ["==", "class", "tree"] to ["==", ["get", "class"], "tree"].
    /// </summary>
    /// <param name="filter">Legacy filter array</param>
    /// <returns>Filter as expression array</returns>
    private static JsonNode? ConvertLegacyFilter(JsonArray filter)
    {
        if (filter[0] is not JsonValue operatorValue || !operatorValue.TryGetValue<string>(out var op))
            throw new ArgumentException("Filter operator must be a string");

        switch (op)
        {
            case "==":
            case "!=":
            case ">":
            case ">=":
            case "<":
            case "<=":
                if (filter.Count != 3)
                    throw new ArgumentException("Filter operator " + op + " must have exactly 2 operands");

                return new JsonArray(op, ToKeyOperand(filter[1]), filter[2]!.DeepClone());
            case "in":
                return new JsonArray(op, ToKeyOperand(filter[1]), new JsonArray("literal", Values(filter, 2)));
            case "!in":
                return new JsonArray("!", new JsonArray("in", ToKeyOperand(filter[1]), new JsonArray("literal", Values(filter, 2))));
            case "has":
                return new JsonArray(op, ToKeyOperand(filter[1]));
            case "!has":
                return new JsonArray("!", new JsonArray(op[1..], ToKeyOperand(filter[1])));
            case "all":
            case "any":
                // An empty filter combination passes all features / no features
                if (filter.Count == 1)
                    return op == "all" ? new JsonArray("literal", true) : new JsonArray("literal", false);
                return ToOperatorArray(op, Children(filter));
            case "none":
                var elements = Children(filter);

                if (elements.Count == 0)
                    return new JsonArray("literal", true);
                return new JsonArray("!", ToOperatorArray("any", elements));
        }

        throw new ArgumentException("Filter operator must be one of \"==\", \"!=\", \">\", \">=\", \"<\", \"<=\", \"in\", \"!in\", \"all\", \"any\", \"none\", \"has\", or \"!has\"");
    }

    /// <summary>
    /// Operand of a legacy filter key: "$type" and "$id" become the global
    /// properties "geometry-type" and "id", every other key a "get" expression.
    /// </summary>
    private static JsonNode ToKeyOperand(JsonNode? key)
    {
        if (key is not JsonValue keyValue || !keyValue.TryGetValue<string>(out var name))
            throw new ArgumentException("Filter expression key must be a string");

        return name switch
        {
            "$type" => new JsonArray("geometry-type"),
            "$id" => new JsonArray("id"),
            _ => new JsonArray("get", name),
        };
    }

    private static JsonArray Values(JsonArray filter, int from)
    {
        var values = new JsonArray();

        for (int i = from; i < filter.Count; i++)
            values.Add(filter[i]?.DeepClone());

        return values;
    }

    /// <summary>
    /// The already converted children of a legacy filter combination. They are
    /// collected in a list (not a JsonArray), because every node gets re-added
    /// to the operator array afterwards and a JsonNode can only have one parent.
    /// </summary>
    private static List<JsonNode?> Children(JsonArray filter)
    {
        var children = new List<JsonNode?>();

        for (int i = 1; i < filter.Count; i++)
        {
            // Existing nodes keep their parent, so nested filter arrays are
            // cloned before the recursive conversion. Children that are already
            // in expression syntax (mixed legacy/expression filters occur in
            // real-world styles) are kept unchanged.
            if (filter[i] is not JsonArray sub)
            {
                children.Add(filter[i]?.DeepClone());
            }
            else if (IsExpressionFilter(sub))
            {
                children.Add((JsonArray)sub.DeepClone());
            }
            else
            {
                children.Add(ConvertLegacyFilter((JsonArray)sub.DeepClone()));
            }
        }

        return children;
    }

    /// <summary>
    /// An expression array [op, child1, child2, ...] for an operator with its
    /// already converted children.
    /// </summary>
    private static JsonArray ToOperatorArray(string op, List<JsonNode?> children)
    {
        var array = new JsonArray(op);

        foreach (var child in children)
            array.Add(child);

        return array;
    }
}
