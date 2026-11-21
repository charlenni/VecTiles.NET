using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Common;

/// <summary>
/// Default implementation of <see cref="IRenderedTile" />. Collects the
/// rendered layer entries of a tile in style file order.
/// </summary>
public class RenderedTile : IRenderedTile
{
    /// <summary>
    /// Create a rendered tile for the given tile.
    /// </summary>
    /// <param name="tile">Tile this rendered tile belongs to.</param>
    public RenderedTile(Tile tile)
    {
        Tile = tile;
        Layers = new List<IRenderedLayerEntry>();
    }

    /// <summary>
    /// Gets the tile this rendered tile was created for.
    /// </summary>
    public Tile Tile { get; private set; }

    /// <summary>
    /// All rendered layers in style file order (bottom layer first): the
    /// entries are added while the style layers of the style file are
    /// processed, geometry layers and symbol layers interleaved.
    /// </summary>
    public IList<IRenderedLayerEntry> Layers { get; private set; }

    /// <summary>
    /// Draws all rendered geometry layers of this tile onto the given canvas.
    /// Symbol layers are skipped; they must be placed and drawn separately.
    /// </summary>
    /// <param name="canvas">Platform specific canvas to draw on (e.g. an SKCanvas).</param>
    /// <param name="context">Context with zoom level and feature used to evaluate paint values.</param>
    public void Draw(object canvas, EvaluationContext context)
    {
        foreach (var entry in Layers)
        {
            if (entry is RenderedGeometryLayer geometryLayer)
            {
                geometryLayer.Renderer.Draw(canvas, context);
            }
        }
    }
}
