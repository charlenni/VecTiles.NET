namespace VecTiles.Styles.Mapsforge.Enums;

/// <summary>
/// Map element type a Mapsforge render theme rule matches (attribute "e" of a rule).
/// </summary>
public enum MapsforgeElement
{
    /// <summary>Matches point features (nodes).</summary>
    Node,
    /// <summary>Matches line and area features (ways).</summary>
    Way,
    /// <summary>Matches all features.</summary>
    Any
}
