using NetTopologySuite.Geometries;
using VecTiles.Common.Primitives;

namespace VecTiles.Converters.MLT.Parser;

/// <summary>
/// Transforms MLT tile coordinates into the normalized 512x512 tile space used by VecTiles.NET,
/// optionally for overzoom (requested tile is a child of the provided tile).
/// </summary>
/// <param name="Scale">Zoom difference between requested and provided tile.</param>
/// <param name="OffsetX">X offset of the requested tile inside the provided tile, in 512-space.</param>
/// <param name="OffsetY">Y offset of the requested tile inside the provided tile, in 512-space.</param>
/// <param name="ExtentFactor">Factor scaling layer coordinates (0..extent) to 512-space.</param>
internal record Overzoom(double Scale, long OffsetX, long OffsetY, double ExtentFactor)
{
    /// <summary>
    /// Size of the normalized output tile space.
    /// </summary>
    private const double TileSize = 512.0;

    /// <summary>
    /// Default MLT layer extent used if a layer provides an invalid extent.
    /// </summary>
    private const uint DefaultExtent = 4096;

    internal static Overzoom None = new Overzoom(1, 0, 0, TileSize / DefaultExtent);

    /// <summary>
    /// Gets an equivalent overzoom transformation for a layer with the given extent.
    /// MLT defines the extent per layer; invalid (zero) extents fall back to the default 4096.
    /// </summary>
    /// <param name="extent">The extent of the layer.</param>
    /// <returns>A transformation scaling the layer coordinates to 512-space.</returns>
    internal Overzoom WithExtent(uint extent)
    {
        return this with { ExtentFactor = TileSize / (extent > 0 ? extent : DefaultExtent) };
    }

    internal Coordinate Transform(long x, long y)
    {
        // Transform from 0..extent-1 to 0..511 and from provided tile to requested tile
        return new Coordinate(x * ExtentFactor * Scale - OffsetX, y * ExtentFactor * Scale - OffsetY);
    }

    internal static Overzoom CreateFromTiles(Tile requestedTile, Tile providedTile)
    {
        var scale = 1 << (requestedTile.Zoom - providedTile.Zoom);
        var offsetX = (requestedTile.X - providedTile.X * scale) * 512;
        var offsetY = (requestedTile.Y - providedTile.Y * scale) * 512;

        if (offsetX < 0 || offsetY < 0)
        {
            throw new ArgumentException("Offset cannot be negative. Ensure that requestedTile is a child of providedTile.");
        }

        if (offsetX >= 512 * scale || offsetY >= 512 * scale)
        {
            throw new ArgumentException("Offset exceeds the scale. Ensure that requestedTile is a child of providedTile.");
        }

        return new Overzoom(scale, offsetX, offsetY, TileSize / DefaultExtent);
    }
}
