namespace VecTiles.Common.Enums;

/// <summary>
/// Sort keys of a style layer for sorting the features of a geometry type
/// ascending by their evaluated sort-key layout property (Mapbox spec:
/// features with a higher key are drawn later and appear on top).
/// </summary>
public enum FeatureSortKey
{
    /// <summary>
    /// Fill sort key (fill-sort-key layout property).
    /// </summary>
    Fill,

    /// <summary>
    /// Line sort key (line-sort-key layout property).
    /// </summary>
    Line,

    /// <summary>
    /// Circle sort key (circle-sort-key layout property).
    /// </summary>
    Circle,
}
