using Newtonsoft.Json.Linq;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

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

    public static IExpression? Parse(JArray array, ExpressionParser parser)
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

    public override object? PossibleOutputs()
    {
        return null;
    }

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

    public override bool Equals(object? obj) => obj is CoalesceExpression other && Equals(other);

    public override int GetHashCode()
    {
        int hashCode = 0;

        foreach (var expression in _expressions)
            hashCode = (hashCode * 397) ^ expression.GetHashCode();

        return hashCode;
    }
}
