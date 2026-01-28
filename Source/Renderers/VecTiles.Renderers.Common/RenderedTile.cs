using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Common;

public class RenderedTile : IRenderedTile
{
    public RenderedTile(Tile tile)
    {
        Tile = tile;
        RenderedLayers = new Dictionary<string, ILayerRenderer>();
        RenderedSymbols = new Dictionary<string, ISymbolLayer>();
    }

    public Tile Tile { get; private set; }

    public IDictionary<string, ILayerRenderer> RenderedLayers { get; private set; }

    public IDictionary<string, ISymbolLayer> RenderedSymbols { get; private set; }

    public void Draw(object canvas, EvaluationContext context)
    {
        foreach (KeyValuePair<string, ILayerRenderer> pair in RenderedLayers)
        {
            var layerName = pair.Key;
            var layer = pair.Value;

            layer.Draw(canvas, context);
        }
    }
}
