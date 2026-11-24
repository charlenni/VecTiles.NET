namespace VecTiles.Styles.Mapsforge.Enums;

/// <summary>
/// Display directive of caption, symbol, lineSymbol and pathText instructions
/// (Mapsforge attribute "display"), defining how they interact with other
/// symbols in the placement.
/// </summary>
public enum MapsforgeDisplay
{
    /// <summary>Displayed when there is space for it (default).</summary>
    IfSpace,
    /// <summary>Always displayed, at the expense of overlapping "ifspace" symbols.</summary>
    Order,
    /// <summary>Always displayed, overlapping other symbols.</summary>
    Always,
    /// <summary>Never displayed.</summary>
    Never
}
