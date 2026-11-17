using NetTopologySuite.Features;
using VecTiles.Common.Interfaces;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Compound filter that returns true only if ALL child filters return true (AND operation).
/// Uses short-circuit evaluation: stops at the first filter that returns false.
/// </summary>
public class AllFilter : CompoundFilter
{
    /// <summary>
    /// Initializes a new all filter with the specified child filters.
    /// </summary>
    /// <param name="filters">The list of filters to combine with AND logic.</param>
    public AllFilter(List<IFilter> filters) : base(filters)
    {
    }

    /// <summary>
    /// Evaluates whether all child filters pass for the given feature.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if all child filters return true; otherwise, false.</returns>
    /// <remarks>
    /// Uses short-circuit evaluation: returns false as soon as any filter fails.
    /// </remarks>
    public override bool Evaluate(IFeature feature)
    {
        // Handle empty filter list - by convention, all() on empty list is true
        if (Filters.Count == 0)
            return true;

        foreach (var filter in Filters)
        {
            if (!filter.Evaluate(feature))
                return false;
        }

        return true;
    }
}
