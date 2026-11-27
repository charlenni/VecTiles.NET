namespace VecTiles.DataSources.PmTiles;

/// <summary>
/// The tile id of a tile in a PMTiles archive: the cumulative position of the tile on the
/// series of Hilbert curves over the tile grids of the zoom levels
/// (see https://github.com/protomaps/PMTiles/blob/main/spec/v3/spec.md, chapter 4.1).
/// </summary>
public static class TileId
{
    /// <summary>
    /// Maximum zoom a tile id can address
    /// </summary>
    public const int MaxZoom = 31;

    /// <summary>
    /// Converts tile coordinates to the tile id on the cumulative Hilbert curve.
    /// </summary>
    /// <param name="zoom">Zoom level of the tile</param>
    /// <param name="x">X coordinate of the tile in the tile grid of the zoom level</param>
    /// <param name="y">Y coordinate of the tile in the tile grid of the zoom level</param>
    /// <returns>The tile id</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the zoom level or the coordinates are out of range</exception>
    public static ulong FromTileCoordinates(int zoom, int x, int y)
    {
        if (zoom is < 0 or > MaxZoom)
            throw new ArgumentOutOfRangeException(nameof(zoom), zoom, $"Zoom must be between 0 and {MaxZoom}");
        if (x is < 0 || x >= 1 << zoom)
            throw new ArgumentOutOfRangeException(nameof(x), x, "X coordinate is out of the tile grid of the zoom level");
        if (y is < 0 || y >= 1 << zoom)
            throw new ArgumentOutOfRangeException(nameof(y), y, "Y coordinate is out of the tile grid of the zoom level");

        // Number of tiles of all zoom levels below this one
        ulong accumulatedTiles = 0;
        for (var level = 0; level < zoom; level++)
            accumulatedTiles += 1ul << (2 * level);

        var dimension = 1ul << zoom;
        ulong distance = 0;
        var tileX = (ulong)x;
        var tileY = (ulong)y;

        for (var scale = dimension >> 1; scale > 0; scale >>= 1)
        {
            var rx = (tileX & scale) > 0 ? 1ul : 0ul;
            var ry = (tileY & scale) > 0 ? 1ul : 0ul;

            distance += scale * scale * ((3 * rx) ^ ry);
            Rotate(dimension, ref tileX, ref tileY, rx, ry);
        }

        return accumulatedTiles + distance;
    }

    /// <summary>
    /// Converts a tile id back to its tile coordinates.
    /// </summary>
    /// <param name="id">The tile id</param>
    /// <returns>Zoom level and X/Y coordinates of the tile</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the tile id has no tile coordinates</exception>
    public static (int Zoom, int X, int Y) ToTileCoordinates(ulong id)
    {
        ulong accumulatedTiles = 0;
        var zoom = 0;
        while (zoom <= MaxZoom)
        {
            var tilesAtZoom = 1ul << (2 * zoom);
            if (accumulatedTiles + tilesAtZoom > id)
            {
                var (x, y) = ToTileCoordinatesOnCurve(1ul << zoom, id - accumulatedTiles);
                return (zoom, checked((int)x), checked((int)y));
            }

            accumulatedTiles += tilesAtZoom;
            zoom++;
        }

        throw new ArgumentOutOfRangeException(nameof(id), id, $"The tile id exceeds the maximum zoom {MaxZoom}");
    }

    /// <summary>
    /// Converts a distance on the Hilbert curve of one zoom level to tile coordinates.
    /// </summary>
    /// <param name="dimension">Number of tiles per row and column of the tile grid</param>
    /// <param name="distance">Distance on the Hilbert curve</param>
    /// <returns>X and Y coordinates of the tile</returns>
    private static (ulong X, ulong Y) ToTileCoordinatesOnCurve(ulong dimension, ulong distance)
    {
        ulong x = 0;
        ulong y = 0;
        var t = distance;

        for (var scale = 1ul; scale < dimension; scale <<= 1)
        {
            var rx = 1ul & (t >> 1);
            var ry = 1ul & (t ^ rx);

            Rotate(scale, ref x, ref y, rx, ry);

            x += scale * rx;
            y += scale * ry;
            t >>= 2;
        }

        return (x, y);
    }

    /// <summary>
    /// Rotates a quadrant of the Hilbert curve.
    /// </summary>
    /// <param name="dimension">Number of tiles per row and column of the current quadrant's grid</param>
    /// <param name="x">X coordinate within the quadrant</param>
    /// <param name="y">Y coordinate within the quadrant</param>
    /// <param name="rx">X position of the current quadrant within its grid</param>
    /// <param name="ry">Y position of the current quadrant within its grid</param>
    private static void Rotate(ulong dimension, ref ulong x, ref ulong y, ulong rx, ulong ry)
    {
        if (ry != 0)
            return;

        if (rx == 1)
        {
            x = dimension - 1 - x;
            y = dimension - 1 - y;
        }

        (x, y) = (y, x);
    }
}
