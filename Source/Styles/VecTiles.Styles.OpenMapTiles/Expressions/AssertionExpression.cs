using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Implements the type assertions "number", "string", "boolean", "object" and
/// "array" like MapLibre Native: ["number", input, fallback...]. If the input
/// doesn't match the type, the fallbacks are tried in order; if none matches,
/// the evaluation fails (null).
/// </summary>
public class AssertionExpression : Expression
{
    private readonly string _type;

    // Item type of the array assertion ("string", "number", "boolean"), optional
    private readonly string? _itemType;

    private readonly IExpression _input;

    private readonly IExpression[] _fallbacks;

    private AssertionExpression(string type, string? itemType, IExpression input, IExpression[] fallbacks)
    {
        _type = type;
        _itemType = itemType;
        _input = input;
        _fallbacks = fallbacks;
    }

    /// <summary>
    /// Parses an assertion expression like ["number", input, fallback...].
    /// </summary>
    /// <param name="array">The JSON array of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
    {
        return Parse(array, 1, parser);
    }

    /// <summary>
    /// Parses an assertion expression from an array that starts at the given
    /// index (the operator name is already consumed), used for nested
    /// assertions like the expected type of "at"/"in".
    /// </summary>
    internal static IExpression? Parse(JsonArray array, int startIndex, ExpressionParser parser)
    {
        var type = array[0]!.GetValue<string>();

        if (array.Count < 2)
        {
            parser.Error($"Expected at least one argument, but only found {array.Count - 1}.");
            return null;
        }

        string? itemType = null;
        int inputIndex = 1;

        // Array assertion with item type: ["array", "number", value]
        if (type == "array" && array.Count >= 3 && array[1] is JsonValue itemTypeValue && itemTypeValue.TryGetValue<string>(out var itemTypeName))
        {
            itemType = itemTypeName;

            inputIndex = 2;
        }

        var input = MatchExpression.ParseChild(array[inputIndex], inputIndex, parser);

        if (input == null)
            return null;

        var fallbacks = new List<IExpression>();

        for (int i = inputIndex + 1; i < array.Count; i++)
        {
            var fallback = MatchExpression.ParseChild(array[i], i, parser);

            if (fallback == null)
                return null;

            fallbacks.Add(fallback);
        }

        if (itemType != null && itemType is not ("string" or "number" or "boolean"))
        {
            parser.Error($"Invalid item type for 'array' assertion: {itemType}.");
            return null;
        }

        return new AssertionExpression(type, itemType, input, [.. fallbacks]);
    }

    /// <summary>
    /// Evaluates the assertion: returns the input value when it matches the
    /// asserted type, otherwise the first fallback value that matches.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>The asserted value, or null if the input and no fallback match.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        var value = _input.Evaluate(ctx);

        if (Matches(value))
            return value;

        foreach (var fallback in _fallbacks)
        {
            var fallbackValue = fallback.Evaluate(ctx);

            if (Matches(fallbackValue))
                return fallbackValue;
        }

        return null;
    }

    /// <summary>
    /// Always returns null: the possible outputs of an assertion cannot be
    /// determined statically.
    /// </summary>
    /// <returns>Null, because the possible outputs are unknown.</returns>
    public override object? PossibleOutputs()
    {
        return null;
    }

    private bool Matches(object? value)
    {
        return value switch
        {
            null => false,
            bool => _type == "boolean",
            _ when ExpressionValueHelper.IsNumber(value) => _type == "number",
            string => _type == "string",
            object?[] array => _type == "array"
                && (_itemType == null || array.All(item => MatchesItem(item))),
            _ => _type == "object"
        };
    }

    private bool MatchesItem(object? value)
    {
        return _itemType switch
        {
            "number" => ExpressionValueHelper.IsNumber(value),
            "string" => value is string,
            "boolean" => value is bool,
            _ => false
        };
    }

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="AssertionExpression"/>, comparing type, item type, input
    /// expression and all fallback expressions.
    /// </summary>
    /// <param name="other">The assertion expression to compare with.</param>
    /// <returns>True if the assertions are equal; otherwise, false.</returns>
    public bool Equals(AssertionExpression? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (_type != other._type || _itemType != other._itemType
            || !_input.Equals(other._input) || _fallbacks.Length != other._fallbacks.Length)
            return false;

        for (int i = 0; i < _fallbacks.Length; i++)
        {
            if (!_fallbacks[i].Equals(other._fallbacks[i]))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AssertionExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = (_type, _itemType).GetHashCode() ^ _input.GetHashCode();

            foreach (var fallback in _fallbacks)
                hashCode = (hashCode * 397) ^ fallback.GetHashCode();

            return hashCode;
        }
    }
}
