// Copyright NetTopologySuite Contributors

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("VecTiles.Common.Primitives.Tests")]
namespace VecTiles.Common.Primitives;

/// <summary>
/// Represents a tile.
/// </summary>
public class Tile
{
    private readonly ulong _id;

    /// <summary>
    /// Creates a new tile from a given id.
    /// </summary>
    /// <param name="id"></param>
    public Tile(ulong id)
    {
        _id = id;

        var (x, y, zoom) = Tile.CalculateTile(id);
        this.X = x;
        this.Y = y;
        this.Zoom = zoom;

        (Left, Bottom, Right, Top) = GetTileBounds(x, y, zoom);
    }

    /// <summary>
    /// Creates a new tile.
    /// </summary>
    public Tile(int x, int y, int zoom)
    {
        this.X = x;
        this.Y = y;
        this.Zoom = zoom;

        (Left, Bottom, Right, Top) = GetTileBounds(x, y, zoom);

        _id = Tile.CalculateTileId(zoom, x, y);
    }

    /// <summary>
    /// The X position of the tile.
    /// </summary>
    public int X { get; init; }

    /// <summary>
    /// The Y position of the tile.
    /// </summary>
    public int Y { get; init; }

    /// <summary>
    /// The zoom level for this tile.
    /// </summary>
    public int Zoom { get; init; }

    /// <summary>
    /// The left position for this tile.
    /// </summary>
    public double Left { get; init; }

    /// <summary>
    /// The top position for this tile.
    /// </summary>
    public double Top { get; init; }

    /// <summary>
    /// The right position for this tile.
    /// </summary>
    public double Right { get; init; }

    /// <summary>
    /// The bottom position for this tile.
    /// </summary>
    public double Bottom { get; init; }

    /// <summary>
    /// Gets the parent tile.
    /// </summary>
    public Tile Parent => new Tile(this.X / 2, this.Y / 2, this.Zoom - 1);

    /// <summary>
    /// Returns a hashcode for this tile position.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
        return this.X.GetHashCode() ^
               this.Y.GetHashCode() ^
               this.Zoom.GetHashCode();
    }

    /// <summary>
    /// Returns true if the given object represents the same tile.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object? obj)
    {
        if (obj is Tile other)
        {
            return other.X == this.X &&
                   other.Y == this.Y &&
                   other.Zoom == this.Zoom;
        }

        return false;
    }

    /// <summary>
    /// Returns a description for this tile.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"{this.X}x-{this.Y}y@{this.Zoom}z";
    }

    /// <summary>
    /// Returns true if the given tiles are direct neighbours.
    /// </summary>
    /// <param name="tileId1">The first tile id.</param>
    /// <param name="tileId2">The second tile id.</param>
    /// <returns></returns>
    public static bool IsDirectNeighbour(ulong tileId1, ulong tileId2)
    {
        if (tileId1 == tileId2) return false;

        (int x1, int y1, int zoom1) = Tile.CalculateTile(tileId1);
        (int x2, int y2, int zoom2) = Tile.CalculateTile(tileId2);

        if (zoom1 != zoom2)
        {
            return false;
        }

        if (x1 == x2)
        {
            return (y1 == y2 + 1) ||
                   (y1 == y2 - 1);
        }
        else if (y1 == y2)
        {
            return (x1 == x2 + 1) ||
                   (x1 == x2 - 1);
        }

        return false;
    }

    /// <summary>
    /// Calculates the tile id of the tile at position (0, 0) for the given zoom.
    /// </summary>
    /// <param name="zoom"></param>
    /// <returns></returns>
    public static ulong CalculateTileId(int zoom)
    {
        switch (zoom)
        {
            case 0:
                return 0;
            case 1:
                return 1;
            case 2:
                return 5;
            case 3:
                return 21;
            case 4:
                return 85;
            case 5:
                return 341;
            case 6:
                return 1365;
            case 7:
                return 5461;
            case 8:
                return 21845;
            case 9:
                return 87381;
            case 10:
                return 349525;
            case 11:
                return 1398101;
            case 12:
                return 5592405;
            case 13:
                return 22369621;
            case 14:
                return 89478485;
            case 15:
                return 357913941;
            case 16:
                return 1431655765;
            case 17:
                return 5726623061;
            case 18:
                return 22906492245;
            case 19:
                return 91625968981;
            case 20:
                return 366503875925;
            case 21:
                return 1466015503701;
            case 22:
                return 5864062014805;
            case 23:
                return 23456248059221;
            case 24:
                return 93824992236885;
        }

        //Calculate the tileId if zoom level doesn't match one of the above precalculated values.
        return (ulong)(Math.Pow(4, zoom) - 1) / 3;
    }

    /// <summary>
    /// Calculates the tile id of the tile at position (x, y) for the given zoom.
    /// </summary>
    /// <param name="zoom"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public static ulong CalculateTileId(int zoom, int x, int y)
    {
        ulong id = Tile.CalculateTileId(zoom);
        long width = (long)(1 << zoom);// System.Math.Pow(2, zoom);
        return id + (ulong)x + (ulong)(y * width);
    }

    /// <summary>
    /// Calculate the tile given the id.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static (int x, int y, int zoom) CalculateTile(ulong id)
    {
        // find out the zoom level first.
        int zoom = 0;
        if (id > 0)
        {
            // only if the id is at least at zoom level 1.
            while (id >= Tile.CalculateTileId(zoom))
            {
                // move to the next zoom level and keep searching.
                zoom++;
            }

            zoom--;
        }

        // calculate the x-y.
        ulong local = id - Tile.CalculateTileId(zoom);
        ulong width = (ulong)(1 << zoom);// System.Math.Pow(2, zoom);
        int x = (int)(local % width);
        int y = (int)(local / width);

        return (x, y, zoom);
    }

    /// <summary>
    /// Returns the id of this tile.
    /// </summary>
    public ulong Id => _id;

    /// <summary>
    /// Returns true if this tile is valid.
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (this.X < 0 || this.Y < 0 || this.Zoom < 0) return false; // some are negative.
            double size = (1 << this.Zoom); //System.Math.Pow(2, this.Zoom);
            return this.X < size && this.Y < size;
        }
    }

    /// <summary>
    /// Returns the subtiles of this tile at the given zoom.
    /// </summary>
    public TileRange GetSubTiles(int zoom)
    {
        if (this.Zoom > zoom)
        {
            throw new ArgumentOutOfRangeException(nameof(zoom),
                "Subtiles can only be calculated for higher zooms.");
        }

        if (this.Zoom == zoom)
        {
            // just return a range of one tile.
            return new TileRange(this.X, this.Y, this.X, this.Y, this.Zoom);
        }

        int factor = 1 << (zoom - this.Zoom);

        return new TileRange(
            this.X * factor,
            this.Y * factor,
            this.X * factor + factor - 1,
            this.Y * factor + factor - 1,
            zoom);
    }

    /// <summary>
    /// Inverts the X-coordinate.
    /// </summary>
    /// <returns></returns>
    public Tile InvertX()
    {
        int n = (int)(1 << this.Zoom);// System.Math.Floor(System.Math.Pow(2, this.Zoom));

        return new Tile(n - this.X - 1, this.Y, this.Zoom);
    }

    /// <summary>
    /// Inverts the Y-coordinate.
    /// </summary>
    /// <returns></returns>
    public Tile InvertY()
    {
        int n = (int)(1 << this.Zoom); //System.Math.Floor(System.Math.Pow(2, this.Zoom));

        return new Tile(this.X, n - this.Y - 1, this.Zoom);
    }
    
    // Earth circumference in EPSG:3857 (Web Mercator)
    private const double EarthCircumference = 40075016.68557848;
    private const double InitialResolution = EarthCircumference / 512.0;

    // Calc four corners of given tile in EPSG:3857 coordinate system
    private static (double minX, double minY, double maxX, double maxY) GetTileBounds(int x, int y, int zoom)
    {
        // Calc resolution pro pixel for this zoom level
        double resolution = InitialResolution / Math.Pow(2, zoom);

        // Calc geographical boarders of tile in EPSG:3857
        double minX = -EarthCircumference / 2.0 + x * 512.0 * resolution;
        double maxX = -EarthCircumference / 2.0 + (x + 1) * 512.0 * resolution;

        double minY = EarthCircumference / 2.0 - (y + 1) * 512.0 * resolution;
        double maxY = EarthCircumference / 2.0 - y * 512.0 * resolution;

        return (minX, minY, maxX, maxY);
    }
}