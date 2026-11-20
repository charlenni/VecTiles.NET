using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

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

    public override object? PossibleOutputs()
    {
        return null;
    }

    public bool Equals(GetOrderExpression? other)
    {
        return other is not null
            && _key == other._key
            && _bound == other._bound
            && _sign == other._sign
            && _allowEqual == other._allowEqual;
    }

    public override bool Equals(object? obj) => obj is GetOrderExpression other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_key, _bound, _sign, _allowEqual);
}
