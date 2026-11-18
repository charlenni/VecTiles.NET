using Newtonsoft.Json.Linq;
using VecTiles.Common.Extensions;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Implements the "get" expression: ["get", key] returns the value of the
/// attribute with the given key from the feature, or null if it doesn't exist.
/// </summary>
public class GetExpression : Expression
{
    private readonly string _key;

    public GetExpression(string key)
    {
        _key = key;
    }

    public static IExpression? Parse(JArray array, ExpressionParser parser)
    {
        if (array.Count != 2 || array[1].Type != JTokenType.String)
        {
            parser.Error("'get' expression requires exactly one string literal argument.");
            return null;
        }

        return new GetExpression(array[1].ToString());
    }

    public override object? Evaluate(EvaluationContext ctx)
    {
        var attributes = ctx.Feature?.Attributes ?? ctx.Attributes;

        if (attributes == null || !attributes.Exists(_key))
            return null;

        return attributes[_key];
    }

    public override object? PossibleOutputs()
    {
        return null;
    }

    public bool Equals(GetExpression? other)
    {
        return other is not null && _key == other._key;
    }

    public override bool Equals(object? obj) => obj is GetExpression other && Equals(other);

    public override int GetHashCode() => _key.GetHashCode();
}
