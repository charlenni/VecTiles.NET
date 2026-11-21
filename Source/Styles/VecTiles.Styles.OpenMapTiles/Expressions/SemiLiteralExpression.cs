using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Implements the "semiliteral" expression: ["semiliteral", array] like
/// MapLibre Native: in contrast to "literal", array elements may also be
/// expressions that are evaluated when the expression runs. ["semiliteral",
/// [["get", "x"], ["get", "y"]]] evaluates to an array with the current
/// attribute values.
/// </summary>
public class SemiLiteralExpression : Expression
{
    private readonly IExpression? _expression;

    private readonly IExpression[]? _elements;

    private SemiLiteralExpression(IExpression? expression, IExpression[]? elements)
    {
        _expression = expression;
        _elements = elements;
    }

    /// <summary>
    /// Parses a "semiliteral" expression from the given JSON array.
    /// </summary>
    /// <param name="array">The JSON array of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
    {
        if (array.Count != 2)
        {
            parser.Error("'semiliteral' expression requires exactly one argument.");
            return null;
        }

        if (array[1] is JsonArray elements)
        {
            var parsedElements = new List<IExpression>();

            for (int i = 0; i < elements.Count; i++)
            {
                var element = parser.Parse(elements[i], i);

                if (element == null)
                    return null;

                parsedElements.Add(element);
            }

            return new SemiLiteralExpression(null, [.. parsedElements]);
        }

        var expression = parser.Parse(array[1], 1);

        if (expression == null)
            return null;

        return new SemiLiteralExpression(expression, null);
    }

    /// <summary>
    /// Evaluates a single expression directly, or evaluates all array elements
    /// into an array of their current values.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>The expression value or the array of evaluated element values.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        if (_elements == null)
            return _expression?.Evaluate(ctx);

        return _elements.Select(element => element.Evaluate(ctx)).ToArray();
    }

    /// <summary>
    /// Always returns null: the possible outputs of a "semiliteral" expression
    /// cannot be determined statically.
    /// </summary>
    /// <returns>Null, because the possible outputs are unknown.</returns>
    public override object? PossibleOutputs()
    {
        return null;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="SemiLiteralExpression"/>, comparing the wrapped expression
    /// or all element expressions.
    /// </summary>
    /// <param name="other">The semiliteral expression to compare with.</param>
    /// <returns>True if the semiliteral expressions are equal; otherwise, false.</returns>
    public bool Equals(SemiLiteralExpression? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (_elements != null && other._elements != null)
        {
            return _elements.Length == other._elements.Length
                && _elements.Zip(other._elements).All(pair => pair.First.Equals(pair.Second));
        }

        return _expression is not null && other._expression is not null && _expression.Equals(other._expression);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SemiLiteralExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int hashCode = 17;

        hashCode = (hashCode * 397) ^ (_expression?.GetHashCode() ?? 0);

        if (_elements != null)
        {
            foreach (var element in _elements)
                hashCode = (hashCode * 31) ^ element.GetHashCode();
        }

        return hashCode;
    }
}
