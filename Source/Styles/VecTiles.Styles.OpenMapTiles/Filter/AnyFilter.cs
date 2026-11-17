using NetTopologySuite.Features;
using VecTiles.Common.Interfaces;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Compound filter that returns true if ANY child filter returns true (OR operation).
/// Uses short-circuit evaluation: stops at the first filter that returns true.
/// </summary>
public class AnyFilter : CompoundFilter
{
    /// <summary>
    /// Initializes a new any filter with the specified child filters.
    /// </summary>
    /// <param name="filters">The list of filters to combine with OR logic.</param>
    public AnyFilter(List<IFilter> filters) : base(filters)
    {
    }

    /// <summary>
    /// Evaluates whether any child filter passes for the given feature.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if any child filter returns true; otherwise, false.</returns>
    /// <remarks>
    /// Uses short-circuit evaluation: returns true as soon as any filter passes.
    /// </remarks>
    public override bool Evaluate(IFeature feature)
    {
        // Handle empty filter list - by convention, any() on empty list is false
        if (Filters.Count == 0)
            return false;

        foreach (var filter in Filters)
        {
            if (filter.Evaluate(feature))
            {
                return true;
            }
        }

        return false;
    }
}
