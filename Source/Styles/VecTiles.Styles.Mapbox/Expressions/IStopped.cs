using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox.Expressions;

/// <summary>
/// Represents an expression that can be evaluated in a given context and provides possible outputs.
/// </summary>
/// <typeparam name="T">The type of the value produced by the expression.</typeparam>
public interface IStopped<T>
{
    /// <summary>
    /// Evaluates the expression using the specified evaluation context.
    /// </summary>
    /// <param name="ctx">The context in which to evaluate the expression.</param>
    /// <returns>The result of the evaluation.</returns>
    T Evaluate(EvaluationContext ctx);

    /// <summary>
    /// Returns the possible outputs that this expression can produce.
    /// </summary>
    /// <returns>The possible output(s) of the expression.</returns>
    T PossibleOutputs();
}
