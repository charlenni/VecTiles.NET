namespace VecTiles.Common.Enums;

/// <summary>
/// Represents the type of geometry for a vector tile feature.
/// </summary>
public enum GeometryType
{
    /// <summary>
    /// A single point geometry.
    /// </summary>
    Point,

    /// <summary>
    /// A line string geometry (a sequence of points forming a line).
    /// </summary>
    LineString,

    /// <summary>
    /// A polygon geometry (a closed shape defined by a sequence of points).
    /// </summary>
    Polygon
}
