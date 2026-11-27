using System.Text.Json.Nodes;
using VecTiles.Common.Extensions;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre.Expressions;

/// <summary>
/// Implements the "get" expression: ["get", key] returns the value of the
/// attribute with the given key from the feature, or null if it doesn't exist.
/// </summary>
public class GetExpression : Expression
{
    private readonly string _key;

    /// <summary>
    /// The attribute key of the expression, for parse-time specializations
    /// like GetEqualsExpression and GetOrderExpression.
    /// </summary>
    public string Key => _key;

    /// <summary>
    /// Initializes a new <see cref="GetExpression"/> for the given attribute key.
    /// </summary>
    /// <param name="key">The attribute key to read from the feature.</param>
    public GetExpression(string key)
    {
        _key = key;
    }

    /// <summary>
    /// Parses a "get" expression from the given JSON array.
    /// </summary>
    /// <param name="array">The JSON array of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
    {
        if (array.Count != 2 || array[1] is not JsonValue keyValue || !keyValue.TryGetValue<string>(out var key))
        {
            parser.Error("'get' expression requires exactly one string literal argument.");
            return null;
        }

        return new GetExpression(key);
    }

    /// <summary>
    /// Evaluates the expression by reading the attribute with the given key
    /// from the feature, or null if it doesn't exist.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>The attribute value with the given key, or null.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        // GetOptionalValue does the dictionary lookup once; Exists + indexer
        // would search twice
        var attributes = ctx.Feature?.Attributes ?? ctx.Attributes;

        return attributes?.GetOptionalValue(_key);
    }

    /// <summary>
    /// Always returns null: the possible outputs of a "get" expression cannot
    /// be determined statically.
    /// </summary>
    /// <returns>Null, because the possible outputs are unknown.</returns>
    public override object? PossibleOutputs()
    {
        return null;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="GetExpression"/>, comparing the attribute keys.
    /// </summary>
    /// <param name="other">The expression to compare with.</param>
    /// <returns>True if the attribute keys are equal; otherwise, false.</returns>
    public bool Equals(GetExpression? other)
    {
        return other is not null && _key == other._key;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GetExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _key.GetHashCode();
}
