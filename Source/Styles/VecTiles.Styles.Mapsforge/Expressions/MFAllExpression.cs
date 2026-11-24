using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapsforge.Expressions;

/// <summary>
/// Conjunction of Mapsforge rule conditions: all conditions of the rule chain
/// leading to a drawing instruction (and the implicit geometry condition of the
/// drawing instruction itself) must match for the style layer to apply.
/// </summary>
public class MFAllExpression : IExpression
{
    private readonly IReadOnlyList<IExpression> _expressions;

    /// <summary>
    /// Gets the empty conjunction, which always evaluates to true.
    /// </summary>
    public static readonly MFAllExpression Empty = new([]);

    /// <summary>
    /// Initializes a conjunction of expressions.
    /// </summary>
    /// <param name="expressions">The expressions, all of which must evaluate to true.</param>
    public MFAllExpression(IEnumerable<IExpression> expressions)
    {
        _expressions = expressions.Where(e => e is not MFAllExpression { IsEmpty: true }).ToList();
    }

    /// <summary>
    /// Gets a value indicating whether the conjunction is empty (and therefore
    /// always true).
    /// </summary>
    public bool IsEmpty => _expressions.Count == 0;

    /// <summary>
    /// Creates a conjunction of this expression with others.
    /// </summary>
    /// <param name="expressions">The other expressions to combine with.</param>
    /// <returns>The combined conjunction.</returns>
    public MFAllExpression And(params IExpression[] expressions)
    {
        return new MFAllExpression(_expressions.Concat(expressions));
    }

    /// <inheritdoc />
    /// <returns><c>true</c> when all sub expressions evaluate to true, otherwise <c>false</c>.</returns>
    public object? Evaluate(EvaluationContext ctx)
    {
        foreach (var expression in _expressions)
        {
            if (expression.Evaluate(ctx) is not true)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public object? PossibleOutputs()
    {
        return new[] { true, false };
    }
}
