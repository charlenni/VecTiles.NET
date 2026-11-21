using System.Text.Json.Nodes;
using VecTiles.Common.Extensions;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Implements the "in" expression: ["in", needle, haystack], where the haystack
/// is a string or an array. Keeps the optimization of the old InFilter: for
/// literal arrays with more than 10 values a HashSet provides O(1) lookups,
/// while small lists are searched linearly (cache locality). Small literal
/// arrays are specialized into typed string[]/double[] arrays on first use, so
/// the common case with 2-4 values doesn't go through the generic AreEqual
/// dispatch for every element. The negated v7 forms ("!in", "!has") stay in
/// the filter system.
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

    // Literal haystacks of 10 values or less, specialized on first evaluation
    // to avoid re-evaluating _haystack and the AreEqual dispatch per element
    private string[]? _stringValues;

    private double[]? _numberValues;

    /// <summary>
    /// Initializes a new <see cref="InExpression"/> with needle and haystack expressions.
    /// </summary>
    /// <param name="needle">The expression yielding the value to search for.</param>
    /// <param name="haystack">The expression yielding the string or array to search in.</param>
    public InExpression(IExpression needle, IExpression haystack)
    {
        _needle = needle;
        _haystack = haystack;
    }

    /// <summary>
    /// Parses an "in" expression from the given JSON array.
    /// </summary>
    /// <param name="array">The JSON array of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
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

    /// <summary>
    /// Evaluates the needle and haystack and returns whether the needle is
    /// contained in the haystack (substring for strings, element for arrays),
    /// using the specialized literal value sets for literal haystacks.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>True if the needle is contained in the haystack; otherwise, false.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        var needle = _needle.Evaluate(ctx);

        // Specialized literal haystacks don't need to be evaluated per feature
        if (_stringValues != null)
            return needle is string stringValue && Contains(_stringValues, stringValue);

        if (_numberValues != null)
        {
            if (needle == null)
                return false;

            var number = ExpressionValueHelper.AsNumber(needle);

            // A non-number can never be equal to a number value (AreEqual)
            return number != null && Contains(_numberValues, number.GetValueOrDefault());
        }

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

        if (haystackArray.Length > HashSetThreshold)
        {
            if (_valueSet == null && _numberSet == null)
            {
                _valueSet = [.. haystackArray.Where(value => value != null && !ExpressionValueHelper.IsNumber(value))];
                _numberSet = [.. haystackArray.Select(value => ExpressionValueHelper.AsNumber(value)).Where(number => number != null).Cast<double>()];
            }

            // HashSet for O(1) lookup if we have more than 10 values (like InFilter)
            if (ExpressionValueHelper.IsNumber(needle))
                return _numberSet!.Contains((double)ExpressionValueHelper.AsNumber(needle)!);

            if (needle is string or bool)
                return _valueSet!.Contains(needle);

            return false;
        }

        if (_stringValues == null && _numberValues == null)
            SpecializeSmallLiteral(haystackArray);

        // Typed fast paths for all-string / all-number literal arrays
        if (_stringValues != null)
            return needle is string stringValue && Contains(_stringValues, stringValue);

        if (_numberValues != null)
        {
            var number = ExpressionValueHelper.AsNumber(needle);
            return number != null && Contains(_numberValues, number.GetValueOrDefault());
        }

        // For small lists, linear search is faster (cache locality)
        for (var i = 0; i < haystackArray.Length; i++)
        {
            if (ExpressionValueHelper.AreEqual(haystackArray[i], needle))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Classifies a small literal array once: all-strings and all-numbers get
    /// typed fast paths, everything else stays on the general AreEqual path.
    /// </summary>
    private void SpecializeSmallLiteral(object?[] haystackArray)
    {
        var allStrings = true;
        var allNumbers = true;

        for (var i = 0; i < haystackArray.Length; i++)
        {
            var value = haystackArray[i];

            if (value == null)
            {
                allStrings = false;
                allNumbers = false;
            }
            else if (value is bool or object?[])
            {
                allStrings = false;
                allNumbers = false;
            }
            else if (value is string)
            {
                allNumbers = false;
            }
            else if (ExpressionValueHelper.IsNumber(value))
            {
                allStrings = false;
            }
            else
            {
                allStrings = false;
                allNumbers = false;
            }
        }

        if (allStrings)
        {
            var values = new string[haystackArray.Length];

            for (var i = 0; i < values.Length; i++)
                values[i] = (string)haystackArray[i]!;

            _stringValues = values;
        }
        else if (allNumbers)
        {
            var values = new double[haystackArray.Length];

            for (var i = 0; i < values.Length; i++)
                values[i] = ExpressionValueHelper.AsNumber(haystackArray[i])!.Value;

            _numberValues = values;
        }
    }

    private static bool Contains(string[] values, string value)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (string.Equals(values[i], value, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool Contains(double[] values, double value)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] == value)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Always returns null: the possible outputs of an "in" expression cannot
    /// be determined statically (the result can be true or false).
    /// </summary>
    /// <returns>Null, because the possible outputs are unknown.</returns>
    public override object? PossibleOutputs()
    {
        return null;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="InExpression"/>, comparing needle and haystack expressions.
    /// </summary>
    /// <param name="other">The in expression to compare with.</param>
    /// <returns>True if the expressions are equal; otherwise, false.</returns>
    public bool Equals(InExpression? other)
    {
        return other is not null
            && (ReferenceEquals(this, other)
                || (_needle.Equals(other._needle) && _haystack.Equals(other._haystack)));
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is InExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => (_needle.GetHashCode() * 397) ^ _haystack.GetHashCode();
}
