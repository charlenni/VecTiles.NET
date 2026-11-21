namespace VecTiles.Common.Enums;

/// <summary>
/// Specifies how the geometry of a layer is painted.
/// </summary>
public enum PaintStyle
{
    /// <summary>
    /// Paints only the filled (interior) part of the geometry.
    /// </summary>
    Fill = 0,
    /// <summary>
    /// Paints only the stroke (outline) of the geometry.
    /// </summary>
    Stroke = 1,
    /// <summary>
    /// Paints both the filled interior and the stroke of the geometry.
    /// </summary>
    StrokeAndFill = 2
}
