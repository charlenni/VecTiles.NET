using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Implements the "match" expression:
/// ["match", input, (label | [labels], output)..., fallback]
/// </summary>
public class MatchExpression : Expression
{
    private readonly IExpression _input;
    private readonly (object?[] Labels, IExpression Output)[] _branches;
    private readonly IExpression _fallback;

    /// <summary>
    /// Initializes a new <see cref="MatchExpression"/> with input, branches and fallback.
    /// </summary>
    /// <param name="input">The expression yielding the value to match against the labels.</param>
    /// <param name="branches">Pairs of label lists and output expressions, evaluated in order.</param>
    /// <param name="fallback">The output used when no label matches the input.</param>
    public MatchExpression(IExpression input, (object?[], IExpression)[] branches, IExpression fallback)
    {
        _input = input;
        _branches = branches;
        _fallback = fallback;
    }

    /// <summary>
    /// Parses a "match" expression from the given JSON array.
    /// </summary>
    /// <param name="array">The JSON array of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
    {
        var length = array.Count;

        // Input + pairs of label/output + fallback:
        // after the input, there is an odd number of elements (2 * pairs + 1 fallback)
        if (length < 5 || (length - 2) % 2 != 1)
        {
            parser.Error("'match' expression requires an input, pairs of label and output and a fallback output.");
            return null;
        }

        var input = ParseChild(array[1], 1, parser);

        if (input == null)
            return null;

        var branches = new List<(object?[], IExpression)>();

        for (int i = 2; i < length - 1; i += 2)
        {
            object?[] labels;

            if (array[i] is JsonArray labelArray)
            {
                labels = labelArray.Select(ToValue).ToArray();

                if (labels.Any(label => label is JsonNode || label is object?[]))
                {
                    parser.Error($"Branch labels of a 'match' expression must be string, number or boolean values: 'match[{i}]'.", i);
                    return null;
                }
            }
            else
            {
                var label = ToValue(array[i]);

                if (label is JsonNode || label is object?[])
                {
                    parser.Error($"Branch labels of a 'match' expression must be string, number or boolean values: 'match[{i}]'.", i);
                    return null;
                }

                labels = [label];
            }

            var output = ParseChild(array[i + 1], i + 1, parser);

            if (output == null)
                return null;

            branches.Add((labels, output));
        }

        var fallback = ParseChild(array[length - 1], length - 1, parser);

        if (fallback == null)
            return null;

        return new MatchExpression(input, [.. branches], fallback);
    }

    /// <summary>
    /// Evaluates the input and returns the output of the first branch whose
    /// labels match the input value, or the fallback output when none matches.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>The value of the first matching branch or of the fallback.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        var value = _input.Evaluate(ctx);

        foreach (var (labels, output) in _branches)
        {
            if (labels.Any(label => Matches(label, value)))
                return output.Evaluate(ctx);
        }

        return _fallback.Evaluate(ctx);
    }

    /// <summary>
    /// Returns the possible outputs of all branch outputs and the fallback.
    /// </summary>
    /// <returns>The possible outputs of the branch and fallback outputs.</returns>
    public override object? PossibleOutputs()
    {
        return _branches.Select(b => b.Output).Append(_fallback).Select(o => o.PossibleOutputs());
    }

    /// <summary>
    /// Parses a child node. Arrays are parsed as expressions, anything else
    /// is taken as a literal value.
    /// </summary>
    internal static IExpression? ParseChild(JsonNode? node, int index, ExpressionParser parser, Type? expected = null)
    {
        if (node is JsonArray)
            return parser.Parse(node, index, expected);

        return new LiteralExpression(ToValue(node));
    }

    /// <summary>
    /// Converts a literal node into a .NET value (number, string, bool or null).
    /// </summary>
    internal static object? ToValue(JsonNode? node)
    {
        if (node == null || node.GetValueKind() == System.Text.Json.JsonValueKind.Null)
            return null;

        if (node is JsonArray array)
            return array.Select(ToValue).ToArray();

        if (node is JsonValue value)
        {
            return value.GetValueKind() switch
            {
                System.Text.Json.JsonValueKind.Number => value.TryGetValue<double>(out var number)
                    ? number
                    : value.GetValue<double>(),
                System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => value.GetValue<bool>(),
                System.Text.Json.JsonValueKind.String => value.GetValue<string>(),
                _ => value.ToJsonString()
            };
        }

        // Objects keep their json text (like JToken.ToString() before)
        return node.ToJsonString();
    }

    private static bool Matches(object? label, object? value)
    {
        if (label == null || value == null)
            return label == null && value == null;

        if (IsNumber(label) && IsNumber(value))
            return Convert.ToDouble(label, System.Globalization.CultureInfo.InvariantCulture)
                == Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);

        if (value is bool valueBool && label is bool labelBool)
            return valueBool == labelBool;

        return string.Equals(
            Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToString(label, System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static bool IsNumber(object? value)
    {
        return value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="MatchExpression"/>, comparing input, all branch labels and
    /// outputs, and the fallback.
    /// </summary>
    /// <param name="other">The match expression to compare with.</param>
    /// <returns>True if the match expressions are equal; otherwise, false.</returns>
    public bool Equals(MatchExpression? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (!_input.Equals(other._input) || _branches.Length != other._branches.Length || !_fallback.Equals(other._fallback))
            return false;

        for (int i = 0; i < _branches.Length; i++)
        {
            var labels = _branches[i].Labels;
            var otherLabels = other._branches[i].Labels;

            if (labels.Length != otherLabels.Length)
                return false;

            for (int j = 0; j < labels.Length; j++)
            {
                if (!ExpressionValueHelper.ValuesEqual(labels[j], otherLabels[j]))
                    return false;
            }

            if (!_branches[i].Output.Equals(other._branches[i].Output))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is MatchExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = _input?.GetHashCode() ?? 0;

            hashCode = (hashCode * 397) ^ (_fallback?.GetHashCode() ?? 0);

            foreach (var (_, output) in _branches)
                hashCode = (hashCode * 397) ^ (output?.GetHashCode() ?? 0);

            return hashCode;
        }
    }
}
