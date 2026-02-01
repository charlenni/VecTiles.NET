using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;

namespace VecTiles.Converters.OpenMapTiles.Parser;

internal record Overzoom(double Scale, long OffsetX, long OffsetY)
{
    internal static Overzoom None = new Overzoom(1, 0, 0);

    internal Coordinate Transform(long x, long y)
    {
        // Transform from 0..4095 to 0..511 and from provided tile to requested tile
        return new Coordinate(x * 0.125 * Scale - OffsetX, y * 0.125 * Scale - OffsetY);
    }

    internal static Overzoom CreateFromTiles(Tile requestedTile, Tile providedTile)
    {
        if (requestedTile.Zoom == providedTile.Zoom)
        {
            return None;
        }

        var scale = 1 << (requestedTile.Zoom - providedTile.Zoom);
        var offsetX = (requestedTile.X - providedTile.X * scale) * 512;
        var offsetY = (requestedTile.Y - providedTile.Y * scale) * 512;
        
        var minX = offsetX;
        var maxX = offsetX + 512;
        var minY = offsetY;
        var maxY = offsetY + 512;
        
        //var partOfProvidedTile = GeometryFactory.Default.ToGeometry(new Envelope(offsetX, offsetX + 512, offsetY, offsetY + 512));

        if (offsetX < 0 || offsetY < 0)
        {
            throw new ArgumentException("Offset cannot be negative. Ensure that requestedTile is a child of providedTile.");
        }
        
        if (offsetX >= 512 * scale || offsetY >= 512 * scale)
        {
            throw new ArgumentException("Offset exceeds the scale. Ensure that requestedTile is a child of providedTile.");
        }

        return new Overzoom(scale, offsetX, offsetY);
    }
}
