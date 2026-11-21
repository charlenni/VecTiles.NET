using VecTiles.Common.Interfaces;

namespace VecTiles.Renderers.Common.Interfaces;

/// <summary>
/// A layer that contains symbols (rendered text or icons) instead of
/// geometry layer data.
/// </summary>
public interface ISymbolLayer
{
    /// <summary>
    /// Gets the symbols of this layer.
    /// </summary>
    List<ISymbol> Symbols { get; }

    /// <summary>
    /// Gets the lowest zoom level at which this layer is drawn.
    /// </summary>
    int MinZoom { get; }

    /// <summary>
    /// Gets the highest zoom level at which this layer is drawn.
    /// </summary>
    int MaxZoom { get; }
}
