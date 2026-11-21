using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Common.Interfaces;

public interface IRenderedTile
{
    public Tile Tile { get; }

    /// <summary>
    /// All rendered layers of the tile in style file order (bottom layer
    /// first): geometry layers and symbol layers interleaved exactly as in
    /// the style file. Implementations must keep this order; the placement
    /// and drawing of symbols relies on it.
    /// </summary>
    public IList<IRenderedLayerEntry> Layers { get; }
}
