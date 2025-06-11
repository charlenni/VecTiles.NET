using NetTopologySuite.Features;

namespace VecTiles.Common.Interfaces;

/// <summary>
/// Defines a filter that evaluates whether a given feature satisfies certain criteria.
/// </summary>
public interface IFilter
{
    /// <summary>
    /// Evaluates the specified feature against the filter criteria.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature matches the filter; otherwise, false.</returns>
    bool Evaluate(IFeature feature);
}
