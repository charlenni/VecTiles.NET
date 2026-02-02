using BruTile;
using VecTiles.Common.Primitives;

namespace VecTiles.Controls.Mapsui.Extensions;

public static class TileInfoExtensions
{
    public static Tile ToTile(this TileInfo tileInfo)
    {
        return new Tile(tileInfo.Index.Col, tileInfo.Index.Row, tileInfo.Index.Level);
    }
}
