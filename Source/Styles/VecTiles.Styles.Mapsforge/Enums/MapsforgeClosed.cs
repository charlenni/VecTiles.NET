namespace VecTiles.Styles.Mapsforge.Enums;

/// <summary>
/// Closed state of the ways a Mapsforge render theme rule matches (attribute "closed" of a rule).
/// </summary>
public enum MapsforgeClosed
{
    /// <summary>Matches closed ways (areas).</summary>
    Yes,
    /// <summary>Matches open ways (lines).</summary>
    No,
    /// <summary>Matches all ways.</summary>
    Any
}
