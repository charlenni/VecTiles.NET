using NetTopologySuite.Features;
using VecTiles.Common.Interfaces;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Compound filter that returns true only if NONE of the child filters return true (NOR operation).
/// Equivalent to NOT (AnyFilter). Uses short-circuit evaluation: stops at the first filter that returns true.
/// </summary>
public class NoneFilter : CompoundFilter
{
    /// <summary>
    /// Initializes a new none filter with the specified child filters.
    /// </summary>
    /// <param name="filters">The list of filters to combine with NOR logic.</param>
    public NoneFilter(List<IFilter> filters) : base(filters)
    {
    }

    /// <summary>
    /// Evaluates whether no child filter passes for the given feature.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if no child filter returns true; otherwise, false.</returns>
    /// <remarks>
    /// Uses short-circuit evaluation: returns false as soon as any filter passes.
    /// </remarks>
    public override bool Evaluate(IFeature feature)
    {
        // Handle empty filter list - by convention, none() on empty list is true
        if (Filters.Count == 0)
            return true;

        foreach (var filter in Filters)
            if (filter.Evaluate(feature))
                return false;

        return true;
    }
}
