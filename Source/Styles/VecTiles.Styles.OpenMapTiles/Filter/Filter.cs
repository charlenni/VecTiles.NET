using NetTopologySuite.Features;
using VecTiles.Common.Interfaces;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Abstract base class for all filters that can be applied to vector tile features.
/// Filters are evaluated for every single feature, so performance is critical.
/// </summary>
public abstract class Filter : IFilter
{
    /// <summary>
    /// Evaluates whether the specified feature passes this filter.
    /// </summary>
    /// <param name="feature">The feature to evaluate. May be null.</param>
    /// <returns>True if the feature passes the filter; otherwise, false.</returns>
    public abstract bool Evaluate(IFeature feature);
}
