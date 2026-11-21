namespace VecTiles.Common.Enums;

/// <summary>
/// Specifies how the joins between segments of a stroked line are drawn.
/// </summary>
public enum StrokeJoin
{
    /// <summary>
    /// Extends the outer edges of the segments until they meet, forming a sharp corner.
    /// </summary>
    Miter,
    /// <summary>
    /// Connects the segments with a circular arc, forming a rounded corner.
    /// </summary>
    Round,
    /// <summary>
    /// Connects the segments with a straight diagonal edge, forming a beveled corner.
    /// </summary>
    Bevel
}
