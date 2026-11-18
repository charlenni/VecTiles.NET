using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

public class Expression : IExpression
{
    /// <summary>
    /// An expression that is always valid. Evaluates to true for every feature,
    /// so all features pass. Use as default for layer filters without a filter.
    /// </summary>
    public static IExpression Empty { get; } = EmptyExpression.Instance;

    public virtual object? Evaluate(EvaluationContext ctx)
    {
        return null;
    }

    public virtual object? PossibleOutputs()
    {
        return null;
    }
}
