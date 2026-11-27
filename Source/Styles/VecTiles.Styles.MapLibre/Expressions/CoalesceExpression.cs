using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre.Expressions;

/// <summary>
/// Implements the "coalesce" expression: ["coalesce", expression...]. Returns
/// the value of the first expression that evaluates to a non-null value.
/// </summary>
public class CoalesceExpression : Expression
{
    private readonly IExpression[] _expressions;

    private CoalesceExpression(IExpression[] expressions)
    {
        _expressions = expressions;
    }

    /// <summary>
    /// Parses a "coalesce" expression from the given JSON array.
    /// </summary>
    /// <param name="array">The JSON array of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
    {
        if (array.Count < 2)
        {
            parser.Error("Expected at least one argument, but only found " + (array.Count - 1) + ".");
            return null;
        }

        var expressions = new List<IExpression>();

        for (int i = 1; i < array.Count; i++)
        {
            var expression = MatchExpression.ParseChild(array[i], i, parser);

            if (expression == null)
                return null;

            expressions.Add(expression);
        }

        return new CoalesceExpression([.. expressions]);
    }

    /// <summary>
    /// Evaluates the expressions in order and returns the first non-null value.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>The first non-null value, or null if all expressions evaluate to null.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        // Stops at the first expression with a non-null value
        foreach (var expression in _expressions)
        {
            var value = expression.Evaluate(ctx);

            if (value != null)
                return value;
        }

        return null;
    }

    /// <summary>
    /// Always returns null: the possible outputs of a "coalesce" expression
    /// cannot be determined statically.
    /// </summary>
    /// <returns>Null, because the possible outputs are unknown.</returns>
    public override object? PossibleOutputs()
    {
        return null;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="CoalesceExpression"/>, comparing all operand expressions.
    /// </summary>
    /// <param name="other">The coalesce expression to compare with.</param>
    /// <returns>True if the coalesce expressions are equal; otherwise, false.</returns>
    public bool Equals(CoalesceExpression? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (_expressions.Length != other._expressions.Length)
            return false;

        for (int i = 0; i < _expressions.Length; i++)
        {
            if (!_expressions[i].Equals(other._expressions[i]))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is CoalesceExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int hashCode = 0;

        foreach (var expression in _expressions)
            hashCode = (hashCode * 397) ^ expression.GetHashCode();

        return hashCode;
    }
}
