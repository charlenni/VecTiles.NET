using NetTopologySuite.Features;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Filter that checks if a feature has a non-empty identifier.
/// </summary>
public class HasIdentifierFilter : Filter
{
    /// <summary>
    /// Singleton instance for performance optimization.
    /// </summary>
    public static readonly HasIdentifierFilter Instance = new HasIdentifierFilter();

    /// <summary>
    /// Initializes a new has identifier filter.
    /// </summary>
    public HasIdentifierFilter()
    {
    }

    /// <summary>
    /// Evaluates whether the feature has an identifier.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature has a non-empty, non-null identifier; otherwise, false.</returns>
    public override bool Evaluate(IFeature feature)
    {
        if (feature == null)
            return false;

        var idObj = feature.Attributes["id"];
        if (idObj == null)
            return false;

        var idString = idObj as string;
        return !string.IsNullOrWhiteSpace(idString);
    }
}
