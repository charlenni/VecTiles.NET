using NetTopologySuite.Geometries;
using VecTiles.Common.Primitives;

namespace VecTiles.Converters.Mapbox.Parser;

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

        var totalParts = 1 << (requestedTile.Zoom - providedTile.Zoom);
        var factor = 512.0 / totalParts;
        var offsetX = requestedTile.X - providedTile.X * totalParts;
        var offsetY = requestedTile.Y - providedTile.Y * totalParts;
        
        var minX = offsetX * factor;
        var maxX = (offsetX + 1) * factor;
        var minY = offsetY * factor;
        var maxY = (offsetY + 1) * factor;
        
        var partOfProvidedTile = GeometryFactory.Default.ToGeometry(new Envelope(minX, maxX, minY, maxY));

        if (offsetX < 0 || offsetY < 0)
        {
            throw new ArgumentException("Offset cannot be negative. Ensure that requestedTile is a child of providedTile.");
        }
        
        if (offsetX >= factor || offsetY >= factor)
        {
            throw new ArgumentException("Offset exceeds the scale. Ensure that requestedTile is a child of providedTile.");
        }

        return new Overzoom(factor, offsetX, offsetY);
    }
}
