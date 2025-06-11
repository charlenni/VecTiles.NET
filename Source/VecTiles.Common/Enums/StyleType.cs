namespace VecTiles.Common.Enums;

/// <summary>
/// Specifies the type of style applied to map features.
/// </summary>
public enum StyleType
{
    /// <summary>
    /// Style is for a tile background.
    /// </summary>
    Background,
    /// <summary>
    /// Style is for a raster source.
    /// </summary>
    Raster,
    /// <summary>
    /// Style is for filling an area.
    /// </summary>
    Fill,
    /// <summary>
    /// Style is for drawing a line.
    /// </summary>
    Line,
    /// <summary>
    /// Style is for drawing a symbol.
    /// </summary>
    Symbol,
    /// <summary>
    /// Style is for drawing buildings in 3D.
    /// </summary>
    FillExtrusion
}
