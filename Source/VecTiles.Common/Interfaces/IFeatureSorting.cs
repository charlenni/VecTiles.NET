using NetTopologySuite.Features;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;

namespace VecTiles.Common.Interfaces;

/// <summary>
/// Sorting the features of a style layer by its layout sort key: styles
/// with data-driven sort keys sort the features ascending by the evaluated
/// key (Mapbox spec: features with a higher key are drawn later and appear
/// on top), styles without sort keys keep the source order.
/// </summary>
public interface IFeatureSorting
{
    /// <summary>
    /// Sorts the features of the layer ascending by the evaluated sort-key
    /// layout property of the given geometry type. Equal keys keep their
    /// source order (stable sort); a constant sort key gives every feature
    /// the same key and keeps the source order.
    /// </summary>
    /// <param name="context">The evaluation context of the tile.</param>
    /// <param name="features">The features of the layer.</param>
    /// <param name="sortKey">The geometry type whose sort-key layout property is evaluated.</param>
    /// <returns>The sorted features.</returns>
    IEnumerable<IFeature> SortFeatures(EvaluationContext context, IEnumerable<IFeature> features, FeatureSortKey sortKey);
}
