using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Base class of the MapLibre style expression implementations. Expressions
/// are parsed from the style JSON and evaluated per feature to compute style
/// property and filter values.
/// </summary>
public class Expression : IExpression
{
    /// <summary>
    /// An expression that is always valid. Evaluates to true for every feature,
    /// so all features pass. Use as default for layer filters without a filter.
    /// </summary>
    public static IExpression Empty { get; } = EmptyExpression.Instance;

    /// <summary>
    /// Evaluates the expression for the given context. The base implementation
    /// always returns null; subclasses implement the actual evaluation.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>The value of the expression, or null if it fails.</returns>
    public virtual object? Evaluate(EvaluationContext ctx)
    {
        return null;
    }

    /// <summary>
    /// Returns the possible outputs of the expression for validation purposes.
    /// The base implementation always returns null, meaning the outputs cannot
    /// be determined statically.
    /// </summary>
    /// <returns>The possible output(s) of the expression, or null.</returns>
    public virtual object? PossibleOutputs()
    {
        return null;
    }
}
