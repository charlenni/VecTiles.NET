namespace VecTiles.Styles.Mapbox.Enums;

/// <summary>
/// Specifies the z-ordering strategy for symbol layers.
/// </summary>
public enum SymbolZOrder
{
    /// <summary>
    /// Automatically determine the z-order based on context.
    /// </summary>
    Auto,

    /// <summary>
    /// Order symbols by their Y position in the viewport (top to bottom).
    /// </summary>
    ViewportY,

    /// <summary>
    /// Order symbols by their source order.
    /// </summary>
    Source
}
