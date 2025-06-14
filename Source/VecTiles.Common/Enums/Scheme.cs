namespace VecTiles.Common.Enums;

/// <summary>
/// Represents the tile addressing scheme.
/// </summary>
public enum Scheme
{
    /// <summary>
    /// TMS tile scheme (Tile Map Service, origin at bottom-left).
    /// </summary>
    Tms,
    /// <summary>
    /// XYZ tile scheme (OpenStreetMap, origin at top-left).
    /// </summary>
    Xyz
}
