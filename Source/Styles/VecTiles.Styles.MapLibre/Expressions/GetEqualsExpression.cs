using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre.Expressions;

/// <summary>
/// Specialized form of ["==", ["get", key], literal] and ["!=", ...] with the
/// arguments in either order, created at parse time. It replaces the
/// CompoundExpression indirection and the generic AreEqual dispatch of the
/// common "attribute equals constant" condition (IdentifierEqualsFilter in the
/// old filter system) with a direct typed comparison.
/// </summary>
public sealed class GetEqualsExpression : Expression
{
    private enum ComparisonKind
    {
        String,

        Number,

        Bool
    }

    private readonly string _key;

    private readonly bool _negated;

    private readonly ComparisonKind _kind;

    private readonly string? _stringValue;

    private readonly double _numberValue;

    private readonly bool _boolValue;

    private GetEqualsExpression(string key, bool negated, ComparisonKind kind, string? stringValue = null, double numberValue = 0d, bool boolValue = false)
    {
        _key = key;
        _negated = negated;
        _kind = kind;
        _stringValue = stringValue;
        _numberValue = numberValue;
        _boolValue = boolValue;
    }

    /// <summary>
    /// Returns a specialized expression for ["=="/"!=", left, right], if one of
    /// the arguments is a "get" and the other a string, number or boolean
    /// literal; otherwise null (null, array and collator variants stay on the
    /// generic CompoundExpression path).
    /// </summary>
    public static IExpression? Create(IExpression left, IExpression right, bool negated)
    {
        if (left is GetExpression get)
            return FromLiteral(get.Key, right, negated);

        if (right is GetExpression otherGet)
            return FromLiteral(otherGet.Key, left, negated);

        return null;
    }

    private static IExpression? FromLiteral(string key, IExpression other, bool negated)
    {
        if (other is not LiteralExpression literal)
            return null;

        var value = literal.Value;

        return value switch
        {
            string stringValue => new GetEqualsExpression(key, negated, ComparisonKind.String, stringValue: stringValue),
            bool boolValue => new GetEqualsExpression(key, negated, ComparisonKind.Bool, boolValue: boolValue),
            _ when ExpressionValueHelper.IsNumber(value) => new GetEqualsExpression(key, negated, ComparisonKind.Number, numberValue: (double)ExpressionValueHelper.AsNumber(value)!),
            _ => null
        };
    }

    /// <summary>
    /// Evaluates the attribute of the feature under the stored key and
    /// compares it to the stored literal, respecting the comparison kind and
    /// the negation flag.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>True if the comparison matches the negation setting; otherwise, false.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        var value = ExpressionValueHelper.GetAttribute(ctx, _key);

        var result = _kind switch
        {
            // Numbers equal numerically, everything else strictly typed
            ComparisonKind.String => value is string stringValue && string.Equals(stringValue, _stringValue, StringComparison.Ordinal),
            ComparisonKind.Number => ExpressionValueHelper.AsNumber(value) == _numberValue,
            _ => value is bool boolValue && boolValue == _boolValue
        };

        return result != _negated;
    }

    /// <summary>
    /// Always returns null: the possible outputs of the comparison cannot be
    /// determined statically (the result can be true or false).
    /// </summary>
    /// <returns>Null, because the possible outputs are unknown.</returns>
    public override object? PossibleOutputs()
    {
        return null;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="GetEqualsExpression"/>, comparing key, negation flag,
    /// comparison kind and literal value.
    /// </summary>
    /// <param name="other">The expression to compare with.</param>
    /// <returns>True if the expressions are equal; otherwise, false.</returns>
    public bool Equals(GetEqualsExpression? other)
    {
        return other is not null
            && _key == other._key
            && _negated == other._negated
            && _kind == other._kind
            && _numberValue == other._numberValue
            && _boolValue == other._boolValue
            && string.Equals(_stringValue, other._stringValue, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GetEqualsExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_key, _negated, _stringValue, _numberValue, _boolValue);
}
