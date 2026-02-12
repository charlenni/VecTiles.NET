using Mapsui;
using Mapsui.Layers;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Controls.Mapsui;

public class RenderedTileFeature : BaseFeature
{
    public RenderedTileFeature(IRenderedTile renderedTile, double left, double top, double right, double bottom)
    {
        RenderedTile = renderedTile;
        //(var left, var top) = SphericalMercator.FromLonLat(RenderedTile.Tile.Left, RenderedTile.Tile.Top);
        //(var right, var bottom) = SphericalMercator.FromLonLat(RenderedTile.Tile.Right, RenderedTile.Tile.Bottom);
        
        if (top > bottom)
            Extent = new MRect(left, bottom, right, top);
        else
            Extent = new MRect(left, top, right, bottom);
    }

    public IRenderedTile RenderedTile { get; private set; }

    public override MRect? Extent { get; }

    public override void CoordinateVisitor(Action<double, double, CoordinateSetter> visit)
    {
        throw new NotImplementedException();
    }

    public override object Clone()
    {
        throw new NotImplementedException();
    }
}
