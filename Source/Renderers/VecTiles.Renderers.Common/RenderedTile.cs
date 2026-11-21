using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Common;

public class RenderedTile : IRenderedTile
{
    public RenderedTile(Tile tile)
    {
        Tile = tile;
        Layers = new List<IRenderedLayerEntry>();
    }

    public Tile Tile { get; private set; }

    /// <summary>
    /// All rendered layers in style file order (bottom layer first): the
    /// entries are added while the style layers of the style file are
    /// processed, geometry layers and symbol layers interleaved.
    /// </summary>
    public IList<IRenderedLayerEntry> Layers { get; private set; }

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
