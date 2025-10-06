using VecTiles.Common.Interfaces;

namespace VecTiles.Renderers.Common.Interfaces;

public interface ISymbolLayer
{
    List<ISymbol> Symbols { get; }

    int MinZoom { get; }

    int MaxZoom { get; }
}
