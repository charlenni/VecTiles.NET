using BruTile;
using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Common;

/// <summary>
/// Extension methods for converting <see cref="TileInfo"/> to <see cref="Tile"/>.
/// </summary>
public static class TileInfoExtensions
{
    /// <summary>
    /// Converts a single <see cref="TileInfo"/> to a <see cref="Tile"/>
    /// </summary>
    /// <param name="tileInfo">The tile info to convert.</param>
    /// <returns>A <see cref="Tile"/> with the same column, row, and level.</returns>
    public static Tile ToTile(this TileInfo tileInfo)
    {
        return new Tile(tileInfo.Index.Col, tileInfo.Index.Row, tileInfo.Index.Level);
    }

    /// <summary>
    /// Converts an array of <see cref="TileInfo"/> to an array of <see cref="Tile"/>
    /// </summary>
    /// <param name="tileInfos">The array of tile infos to convert.</param>
    /// <returns>An array of <see cref="Tile"/> objects.</returns>
    public static Tile[] ToTile(this TileInfo[] tileInfos) =>
        Array.ConvertAll(tileInfos, t => t.ToTile());

    /// <summary>
    /// Converts a collection of <see cref="TileInfo"/> to an array of <see cref="Tile"/>
    /// </summary>
    /// <param name="tileInfos">The collection of tile infos to convert.</param>
    /// <returns>An array of <see cref="Tile"/> objects.</returns>
    public static Tile[] ToTile(this IEnumerable<TileInfo> tileInfos) =>
        tileInfos.Select(t => t.ToTile()).ToArray();
}
