using Mapsui.Styles;
using VecTiles.Renderers.Common;

namespace VecTiles.Controls.Mapsui;

public class RenderedTileStyle : IStyle
{
    public RenderedTileStyle(TileInformation tileInformation = null)
    {
        TileInformation = tileInformation;
    }

    public TileInformation TileInformation { get; private set; }

    public double MinVisible { get; set; } = 0;

    public double MaxVisible { get; set; } = double.MaxValue;

    public bool Enabled { get; set; } = true;

    public float Opacity { get; set; } = 1.0f;
}
