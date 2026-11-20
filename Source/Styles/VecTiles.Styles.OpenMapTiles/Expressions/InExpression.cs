using System.Text.Json.Nodes;
using VecTiles.Common.Extensions;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Implements the "in" expression: ["in", needle, haystack], where the haystack
/// is a string or an array. Keeps the optimization of the old InFilter: for
/// literal arrays with more than 10 values a HashSet provides O(1) lookups,
/// while small lists are searched linearly (cache locality).
/// The negated v7 forms ("!in", "!has") stay in the filter system.
/// </summary>
public class InExpression : Expression
{
    private const int HashSetThreshold = 10;

    private readonly IExpression _needle;

    private readonly IExpression _haystack;

    // Lazy HashSet for O(1) lookups, only for literal arrays and only if
    // there are more than 10 values (like InFilter). Values are bucketed by
    // type, so an int lookup finds a double value.
    private HashSet<object>? _valueSet;

    private HashSet<double>? _numberSet;

    public InExpression(IExpression needle, IExpression haystack)
    {
        _needle = needle;
        _haystack = haystack;
    }

    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
    {
        if (array.Count != 3)
        {
            parser.Error("Expected 2 arguments, but found " + (array.Count - 1) + " instead.");
            return null;
        }

        var needle = MatchExpression.ParseChild(array[1], 1, parser);
        var haystack = MatchExpression.ParseChild(array[2], 2, parser);

        if (needle == null || haystack == null)
            return null;

        return new InExpression(needle, haystack);
    }

    public override object? Evaluate(EvaluationContext ctx)
    {
        var needle = _needle.Evaluate(ctx);

        switch (_haystack.Evaluate(ctx))
        {
            case string haystackString:
                return needle != null && haystackString.Contains(ExpressionValueHelper.ToStringValue(needle), StringComparison.Ordinal);

            case object?[] haystackArray:
                return InArray(needle, haystackArray);

            default:
                return null;
        }
    }

    private bool InArray(object? needle, object?[] haystackArray)
    {
        if (needle == null)
            return false;

        if (_valueSet == null && haystackArray.Length > HashSetThreshold)
        {
            _valueSet = [.. haystackArray.Where(value => value != null && !ExpressionValueHelper.IsNumber(value))];
            _numberSet = [.. haystackArray.Select(value => ExpressionValueHelper.AsNumber(value)).Where(number => number != null).Cast<double>()];
        }

        // HashSet for O(1) lookup if we have more than 10 values (like InFilter)
        if (_valueSet != null || _numberSet != null)
        {
            if (ExpressionValueHelper.IsNumber(needle))
                return _numberSet!.Contains((double)ExpressionValueHelper.AsNumber(needle)!);

            if (needle is string or bool)
                return _valueSet!.Contains(needle);

            return false;
        }

        // For small lists, linear search is faster (cache locality)
        for (var i = 0; i < haystackArray.Length; i++)
        {
            if (ExpressionValueHelper.AreEqual(haystackArray[i], needle))
                return true;
        }

        return false;
    }

    public override object? PossibleOutputs()
    {
        return null;
    }

    public bool Equals(InExpression? other)
    {
        return other is not null
            && (ReferenceEquals(this, other)
                || (_needle.Equals(other._needle) && _haystack.Equals(other._haystack)));
    }

    public override bool Equals(object? obj) => obj is InExpression other && Equals(other);

    public override int GetHashCode() => (_needle.GetHashCode() * 397) ^ _haystack.GetHashCode();
}
