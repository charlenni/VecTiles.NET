namespace VecTiles.Common.Enums;

/// <summary>
/// Specifies the alignment mode for the map.
/// </summary>
public enum MapAlignment
{
    /// <summary>
    /// Align relative to the map.
    /// </summary>
    Map,

    /// <summary>
    /// Align relative to the viewport (screen).
    /// </summary>
    Viewport,

    /// <summary>
    /// Automatically determine the best alignment dependent on other values.
    /// </summary>
    Auto
}
