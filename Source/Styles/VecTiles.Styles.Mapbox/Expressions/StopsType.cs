namespace VecTiles.Styles.Mapbox.Expressions;

/// <summary>
/// Specifies the type of stops used in Mapbox expressions.
/// </summary>
public enum StopsType
{
    /// <summary>
    /// Exponential stops interpolate values exponentially between stop points.
    /// </summary>
    Exponential,

    /// <summary>
    /// Interval stops use discrete intervals between stop points.
    /// </summary>
    Interval,

    /// <summary>
    /// Categorical stops map specific categories to output values.
    /// </summary>
    Categorical
}
