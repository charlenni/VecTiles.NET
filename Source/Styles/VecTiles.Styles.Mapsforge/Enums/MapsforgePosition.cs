namespace VecTiles.Styles.Mapsforge.Enums;

/// <summary>
/// Position of a caption relative to its referenced symbol or of a symbol
/// relative to the feature point (Mapsforge attribute "position").
/// </summary>
public enum MapsforgePosition
{
    /// <summary>The render engine selects the position (rendered centered here).</summary>
    Auto,
    /// <summary>Centered on the feature point / on the symbol.</summary>
    Center,
    /// <summary>Below the symbol, centered.</summary>
    Below,
    /// <summary>Below the symbol, right aligned to its left edge.</summary>
    BelowLeft,
    /// <summary>Below the symbol, left aligned to its right edge.</summary>
    BelowRight,
    /// <summary>Above the symbol, centered.</summary>
    Above,
    /// <summary>Above the symbol, right aligned to its left edge.</summary>
    AboveLeft,
    /// <summary>Above the symbol, left aligned to its right edge.</summary>
    AboveRight,
    /// <summary>Left of the symbol, vertically centered.</summary>
    Left,
    /// <summary>Right of the symbol, vertically centered.</summary>
    Right
}
