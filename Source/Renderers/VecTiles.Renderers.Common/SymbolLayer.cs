using VecTiles.Common.Interfaces;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Common;

/// <summary>
/// Default implementation of <see cref="ISymbolLayer" />. Holds the symbols
/// of a symbol layer and its zoom range. Copy constructor support copies
/// symbols from an existing <see cref="ISymbolLayer" />.
/// </summary>
public class SymbolLayer : ISymbolLayer
{
    readonly List<ISymbol> _symbols = new List<ISymbol>();

    /// <summary>
    /// Create a symbol layer that copies the symbols from an existing symbol layer.
    /// </summary>
    /// <param name="symbolLayer">Symbol layer to copy the symbols from, or null for an empty layer.</param>
    /// <param name="minZoom">Lowest zoom level at which this layer is drawn.</param>
    /// <param name="maxZoom">Highest zoom level at which this layer is drawn.</param>
    public SymbolLayer(ISymbolLayer? symbolLayer = null, int minZoom = 0, int maxZoom = 24) : this(symbolLayer?.Symbols, minZoom, maxZoom)
    {
    }

    /// <summary>
    /// Create a symbol layer from the given symbols.
    /// </summary>
    /// <param name="symbols">Symbols to add to this layer, or null for an empty layer.</param>
    /// <param name="minZoom">Lowest zoom level at which this layer is drawn.</param>
    /// <param name="maxZoom">Highest zoom level at which this layer is drawn.</param>
    public SymbolLayer(IEnumerable<ISymbol>? symbols = null, int minZoom = 0, int maxZoom = 24)
    {
        if (symbols != null)
        {
            _symbols.AddRange(symbols);

            MinZoom = minZoom;
            MaxZoom = maxZoom;
        }
    }

    /// <summary>
    /// Gets the symbols of this layer.
    /// </summary>
    public List<ISymbol> Symbols => _symbols;

    /// <summary>
    /// Gets the lowest zoom level at which this layer is drawn.
    /// </summary>
    public int MinZoom { get; }

    /// <summary>
    /// Gets the highest zoom level at which this layer is drawn.
    /// </summary>
    public int MaxZoom { get; }
}
