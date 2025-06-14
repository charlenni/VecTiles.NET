namespace VecTiles.Common.Enums;

/// <summary>
/// Specifies the placement of a symbol relative to its geometry.
/// </summary>
public enum SymbolPlacement
{
    /// <summary>
    /// Symbol is placed at the point where the geometry is located.
    /// </summary>
    Point,
    /// <summary>
    /// Symbol is placed along the geometry line.
    /// </summary>
    Line,
    /// <summary>
    /// Symbol is placed at the center of the geometry line.
    /// </summary>
    LineCenter
}
