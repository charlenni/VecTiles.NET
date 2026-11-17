using NetTopologySuite.Features;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Filter that checks if a feature does NOT have an identifier or has an empty/whitespace identifier.
/// </summary>
public class NotHasIdentifierFilter : Filter
{
    /// <summary>
    /// Singleton instance for performance optimization.
    /// </summary>
    public static readonly NotHasIdentifierFilter Instance = new NotHasIdentifierFilter();

    /// <summary>
    /// Initializes a new not has identifier filter.
    /// </summary>
    public NotHasIdentifierFilter()
    {
    }

    /// <summary>
    /// Evaluates whether the feature does not have an identifier.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature has no identifier, a null identifier, or a whitespace-only identifier; otherwise, false.</returns>
    public override bool Evaluate(IFeature feature)
    {
        if (feature == null)
            return true;

        var idObj = feature.Attributes["id"];
        if (idObj == null)
            return true;

        var idString = idObj as string;
        return string.IsNullOrWhiteSpace(idString);
    }
}
