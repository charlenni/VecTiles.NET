using VecTiles.Common.Interfaces;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Common;

public class SymbolLayer : ISymbolLayer
{
    readonly List<ISymbol> _symbols = new List<ISymbol>();

    public SymbolLayer(ISymbolLayer? symbolLayer = null, int minZoom = 0, int maxZoom = 24) : this(symbolLayer?.Symbols, minZoom, maxZoom)
    {
    }

    public SymbolLayer(IEnumerable<ISymbol>? symbols = null, int minZoom = 0, int maxZoom = 24)
    {
        if (symbols != null)
        {
            _symbols.AddRange(symbols);

            MinZoom = minZoom;
            MaxZoom = maxZoom;
        }
    }

    public List<ISymbol> Symbols => _symbols;

    public int MinZoom { get; }

    public int MaxZoom { get; }
}
