using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Common.Interfaces;

/// <summary>
/// A tile that has been rendered for the current zoom level. It holds the
/// tile it was rendered for and the rendered layer entries in style file order.
/// </summary>
public interface IRenderedTile
{
    /// <summary>
    /// Gets the tile this rendered tile was created for.
    /// </summary>
    public Tile Tile { get; }

    /// <summary>
    /// All rendered layers of the tile in style file order (bottom layer
    /// first): geometry layers and symbol layers interleaved exactly as in
    /// the style file. Implementations must keep this order; the placement
    /// and drawing of symbols relies on it.
    /// </summary>
    public IList<IRenderedLayerEntry> Layers { get; }
}
