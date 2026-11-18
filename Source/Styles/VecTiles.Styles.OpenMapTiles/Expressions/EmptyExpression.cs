using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// An expression that is always valid. Evaluates to true for every feature, so no
/// feature is filtered out. Used as default filter for layers without a filter.
/// </summary>
public sealed class EmptyExpression : Expression
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly EmptyExpression Instance = new EmptyExpression();

    private EmptyExpression()
    {
    }

    /// <summary>
    /// Evaluates the expression - always returns true.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>Always returns true.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        return true;
    }

    /// <summary>
    /// Returns the only possible output - true.
    /// </summary>
    /// <returns>Always returns true.</returns>
    public override object? PossibleOutputs()
    {
        return true;
    }
}
