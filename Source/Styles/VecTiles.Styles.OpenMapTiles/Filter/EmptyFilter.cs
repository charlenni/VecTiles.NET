using NetTopologySuite.Features;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// A filter that always returns true, effectively passing all features.
/// This is used as a placeholder when no filtering is required.
/// </summary>
public class EmptyFilter : Filter
{
    /// <summary>
    /// Singleton instance for performance optimization.
    /// </summary>
    public static readonly EmptyFilter Instance = new EmptyFilter();

    /// <summary>
    /// Evaluates the feature - always returns true.
    /// </summary>
    /// <param name="feature">The feature to evaluate (can be null).</param>
    /// <returns>Always returns true.</returns>
    public override bool Evaluate(IFeature feature)
    {
        return true;
    }
}
