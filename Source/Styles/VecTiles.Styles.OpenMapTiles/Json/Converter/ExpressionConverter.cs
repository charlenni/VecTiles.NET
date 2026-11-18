using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VecTiles.Common.Interfaces;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.OpenMapTiles.Json.Converter;

public class ExpressionConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        //  objectType == typeof(JsonStoppedString) || objectType == typeof(string);
        return true;
    }

    public override bool CanRead => true;

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        // A missing/undefined filter is valid for all features
        if (reader.TokenType == JsonToken.Null)
            return objectType == typeof(IExpression) ? Expression.Empty : null;

        JToken token = JToken.Load(reader);

        switch (token.Type)
        {
            case JTokenType.Object:
                // It is a object, so we assume, that it is a stopped type
                if (objectType.GenericTypeArguments.ToString() == "string")
                    return CreateStoppedString(token);
                break;
            case JTokenType.Array:
                // We have an array, so we assume, that it is an expression
                if (objectType == typeof(IExpression))
                    return ConvertFilter((JArray)token);
                break;
        }

        return null;
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Convert a layer filter to an expression. Filters in expression syntax are
    /// parsed directly, legacy filter arrays are converted to expression syntax first.
    /// </summary>
    /// <param name="filter">JArray with the layer filter</param>
    /// <returns>An expression that evaluates to a boolean for every feature</returns>
    public static IExpression ConvertFilter(JArray filter)
    {
        // An empty filter is valid for all features
        if (filter == null || filter.Count == 0)
            return Expression.Empty;

        var token = IsExpressionFilter(filter) ? filter : ConvertLegacyFilter(filter);
        var expression = ExpressionParser.ParseExpression(token, typeof(bool));

        if (expression == null)
            throw new ArgumentException(nameof(filter), new FormatException("Couldn't parse filter: " + token.ToString(Formatting.None)));

        return expression;
    }

    public StoppedString CreateStoppedString(JToken token)
    {
        var stoppedString = new StoppedString { Stops = new List<KeyValuePair<float, string>>() };

        JToken? baseToken = token.SelectToken("base");

        if (baseToken != null)
            stoppedString.Base = baseToken.ToObject<float>();
        else
            stoppedString.Base = 1f;

        JToken? stops = token.SelectToken("stops");

        if (stops == null)
            return stoppedString;

        foreach (var stop in stops)
        {
            var zoom = stop?.First?.ToObject<float>() ?? 0.0f;
            var text = stop?.Last?.ToObject<string>() ?? string.Empty;
            stoppedString.Stops.Add(new KeyValuePair<float, string>(zoom, text));
        }

        return stoppedString;
    }

    public static bool IsExpressionFilter(JArray filter)
    {
        if (filter == null || filter.Count == 0)
            return false;

        if (filter[0].Type != JTokenType.String)
            return false;

        var op = filter[0].ToString();

        switch (op)
        {
            case "has":
                if (filter.Count < 2)
                    return false;
                var operand = filter[1].ToString();
                return operand != "$id" && operand != "$type";
            case "in":
            case "!in":
            case "!has":
            case "none":
                return false;
            case "==":
            case "!=":
            case ">":
            case ">=":
            case "<":
            case "<=":
                return filter.Count == 3 && (filter[1].Type == JTokenType.Array || filter[2].Type == JTokenType.Array);
            case "any":
            case "all":
                // If we only have an "all" as filter
                if (filter.Count == 1)
                    return false;
                for (int i = 1; i < filter.Count; i++)
                {
                    if (!IsExpressionFilter(filter[i] as JArray) && filter.Type != JTokenType.Boolean)
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
    private static JToken ConvertLegacyFilter(JArray filter)
    {
        if (filter[0].Type != JTokenType.String)
            throw new ArgumentException("Filter operator must be a string");

        var op = filter[0].ToString();

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

                return new JArray(op, ToKeyOperand(filter[1]), filter[2]);
            case "in":
                return new JArray(op, ToKeyOperand(filter[1]), new JArray("literal", Values(filter, 2)));
            case "!in":
                return new JArray("!", new JArray("in", ToKeyOperand(filter[1]), new JArray("literal", Values(filter, 2))));
            case "has":
                return new JArray(op, ToKeyOperand(filter[1]));
            case "!has":
                return new JArray("!", new JArray(op[1..], ToKeyOperand(filter[1])));
            case "all":
            case "any":
                // An empty filter combination passes all features / no features
                if (filter.Count == 1)
                    return op == "all" ? new JArray("literal", true) : new JArray("literal", false);
                return ToOperatorArray(op, Children(filter));
            case "none":
                var elements = Children(filter);

                if (elements.Count == 0)
                    return new JArray("literal", true);
                return new JArray("!", ToOperatorArray("any", elements));
        }

        throw new ArgumentException("Filter operator must be one of \"==\", \"!=\", \">\", \">=\", \"<\", \"<=\", \"in\", \"!in\", \"all\", \"any\", \"none\", \"has\", or \"!has\"");
    }

    /// <summary>
    /// Operand of a legacy filter key: "$type" and "$id" become the global
    /// properties "geometry-type" and "id", every other key a "get" expression.
    /// </summary>
    private static JToken ToKeyOperand(JToken key)
    {
        if (key.Type != JTokenType.String)
            throw new ArgumentException("Filter expression key must be a string");

        var name = key.ToString();

        return name switch
        {
            "$type" => new JArray("geometry-type"),
            "$id" => new JArray("id"),
            _ => new JArray("get", name),
        };
    }

    private static JArray Values(JArray filter, int from)
    {
        var values = new JArray();

        for (int i = from; i < filter.Count; i++)
            values.Add(filter[i]);

        return values;
    }

    private static JArray Children(JArray filter)
    {
        var children = new JArray();

        for (int i = 1; i < filter.Count; i++)
            children.Add(filter[i] is JArray sub ? ConvertLegacyFilter(sub) : filter[i]);

        return children;
    }

    /// <summary>
    /// An expression array [op, child1, child2, ...] for an operator with its
    /// already converted children.
    /// </summary>
    private static JArray ToOperatorArray(string op, JArray children)
    {
        var array = new JArray();
        array.Add(op);

        foreach (var child in children)
            array.Add(child);

        return array;
    }
}