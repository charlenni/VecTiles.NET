using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Common.Interfaces;

public interface IRenderedTile
{
    public Tile Tile { get; }

    public IDictionary<string, IStyledLayer> RenderedLayers { get; }

    public IDictionary<string, ISymbolLayer> RenderedSymbols { get; }
}
