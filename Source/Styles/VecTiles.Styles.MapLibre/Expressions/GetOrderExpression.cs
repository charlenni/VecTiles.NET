using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre.Expressions;

/// <summary>
/// Specialized form of ["<"/"<="/">"/">=", ["get", key], literal-number] with
/// the number literal on either side, created at parse time. It replaces the
/// CompoundExpression indirection and the generic Compare dispatch of the
/// common "attribute order constant" condition (BinaryFilter in the old
/// filter system) with a direct double comparison.
/// </summary>
public sealed class GetOrderExpression : Expression
{
    private readonly string _key;

    private readonly double _bound;

    // -1: value < bound (<, <=), +1: value > bound (>, >=), like the sign of
    // EvaluateOrder; flipped if the "get" is on the right side
    private readonly int _sign;

    private readonly bool _allowEqual;

    private GetOrderExpression(string key, double bound, int sign, bool allowEqual)
    {
        _key = key;
        _bound = bound;
        _sign = sign;
        _allowEqual = allowEqual;
    }

    /// <summary>
    /// Returns a specialized expression for the order comparisons with a "get"
    /// and a number literal on one side, or null if the arguments don't match
    /// the shape (strings, collator variants and other types stay on the
    /// generic CompoundExpression path).
    /// </summary>
    public static IExpression? Create(IExpression left, IExpression right, int sign, bool allowEqual)
    {
        if (left is GetExpression get && right is LiteralExpression literal && ExpressionValueHelper.IsNumber(literal.Value))
            return new GetOrderExpression(get.Key, (double)ExpressionValueHelper.AsNumber(literal.Value)!, sign, allowEqual);

        if (right is GetExpression otherGet && left is LiteralExpression otherLiteral && ExpressionValueHelper.IsNumber(otherLiteral.Value))
            return new GetOrderExpression(otherGet.Key, (double)ExpressionValueHelper.AsNumber(otherLiteral.Value)!, -sign, allowEqual);

        return null;
    }

    /// <summary>
    /// Evaluates the attribute of the feature under the stored key and
    /// compares it to the stored bound, respecting the direction, equality
    /// tolerance and negation-free comparison semantics of the operator.
    /// Non-number attribute values are incomparable and evaluate to false.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>True if the comparison matches the operator; otherwise, false.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        // A non-number attribute value is incomparable with the bound, like
        // the generic path returning false for incomparable types
        var number = ExpressionValueHelper.AsNumber(ExpressionValueHelper.GetAttribute(ctx, _key));

        if (number == null)
            return false;

        var comparison = number.GetValueOrDefault() < _bound ? -1 : number.GetValueOrDefault() > _bound ? 1 : 0;

        return _allowEqual ? comparison * _sign >= 0 : comparison == _sign;
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
    /// <see cref="GetOrderExpression"/>, comparing key, bound, direction and
    /// equality tolerance.
    /// </summary>
    /// <param name="other">The expression to compare with.</param>
    /// <returns>True if the expressions are equal; otherwise, false.</returns>
    public bool Equals(GetOrderExpression? other)
    {
        return other is not null
            && _key == other._key
            && _bound == other._bound
            && _sign == other._sign
            && _allowEqual == other._allowEqual;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GetOrderExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_key, _bound, _sign, _allowEqual);
}
