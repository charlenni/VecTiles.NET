using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.MapLibre.Expressions;

/// <summary>
/// An expression wrapping a literal JSON value, which evaluates to the value
/// itself without further computation.
/// </summary>
public sealed class LiteralExpression : IExpression
{
    private readonly object? _value;

    /// <summary>
    /// The literal value of the expression, for parse-time specializations
    /// like GetEqualsExpression and GetOrderExpression.
    /// </summary>
    public object? Value => _value;

    /// <summary>
    /// Initializes a new <see cref="LiteralExpression"/> wrapping the given value.
    /// </summary>
    /// <param name="value">The literal value of the expression.</param>
    public LiteralExpression(object? value) => _value = value;

    /// <summary>
    /// Parses a "literal" expression from the given JSON array.
    /// </summary>
    /// <param name="array">The JSON array of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
    {
        if (array.Count != 2)
        {
            parser.Error("'literal' expression requires exactly one argument.");
            return null;
        }

        return new LiteralExpression(MatchExpression.ToValue(array[1]));
    }

    /// <summary>
    /// Evaluates the expression - always returns the literal value.
    /// </summary>
    /// <param name="ctx">The evaluation context (ignored).</param>
    /// <returns>Always returns the literal value.</returns>
    public object? Evaluate(EvaluationContext ctx) => _value;

    /// <summary>
    /// Returns the possible outputs of this expression - the literal value itself.
    /// </summary>
    /// <returns>Always returns the literal value.</returns>
    public object? PossibleOutputs() => _value;

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="LiteralExpression"/>, comparing the wrapped values.
    /// </summary>
    /// <param name="other">The literal expression to compare with.</param>
    /// <returns>True if the literal values are equal; otherwise, false.</returns>
    public bool Equals(LiteralExpression? other)
    {
        return other is not null && (ReferenceEquals(this, other) || ExpressionValueHelper.ValuesEqual(_value, other._value));
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is LiteralExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => ExpressionValueHelper.ValueHashCode(_value);
}
