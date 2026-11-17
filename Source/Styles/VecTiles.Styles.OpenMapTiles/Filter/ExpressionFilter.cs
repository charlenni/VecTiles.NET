using NetTopologySuite.Features;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Placeholder filter for expression-based filtering.
/// Currently always returns false. This will be replaced by actual expression evaluation in future versions.
/// </summary>
public class ExpressionFilter : Filter
{
    /// <summary>
    /// Evaluates the feature - currently always returns false.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>Always returns false (placeholder implementation).</returns>
    public override bool Evaluate(IFeature feature)
    {
        return false;
    }
}
