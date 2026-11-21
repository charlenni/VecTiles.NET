using VecTiles.Common.Primitives;

namespace VecTiles.Common.Interfaces;

/// <summary>
/// Represents a style expression that can be evaluated for a feature to produce a style property value.
/// </summary>
public interface IExpression
{
    /// <summary>
    /// Evaluates the expression in the given context.
    /// </summary>
    /// <param name="ctx">The evaluation context with zoom, feature and its attributes.</param>
    /// <returns>The evaluated value of the expression.</returns>
    object? Evaluate(EvaluationContext ctx);

    /// <summary>
    /// Returns the possible output values of this expression.
    /// </summary>
    /// <returns>A collection of the possible outputs, or <c>null</c> if the outputs cannot be determined.</returns>
    object? PossibleOutputs();
}
