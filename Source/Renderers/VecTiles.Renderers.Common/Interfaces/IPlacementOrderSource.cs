namespace VecTiles.Renderers.Common.Interfaces;

/// <summary>
/// Optional interface of a rendered tile source whose symbols follow the
/// mapsforge render theme semantics: the placement pass orders the candidates
/// by the render priority (attribute priority of caption, symbol, lineSymbol
/// and pathText, see the SortOrder of the symbols) and not by the style layer
/// order (OpenMapTiles semantics).
/// </summary>
public interface IPlacementOrderSource
{
    /// <summary>
    /// Gets a value indicating whether the placement pass orders the
    /// candidates by the render priority.
    /// </summary>
    bool OrdersPlacementByPriority { get; }
}
