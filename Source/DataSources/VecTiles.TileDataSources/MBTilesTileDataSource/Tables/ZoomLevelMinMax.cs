using SQLite;
using VecTiles.Common.Primitives;

namespace VecTiles.DataSources.MbTiles.Tables;

/// <summary>
/// Represents the minimum and maximum tile X and Y values for one zoom level queried from the 'tiles' table of an MBTiles database.
/// </summary>
[Table("tiles")]
public class ZoomLevelMinMax
{
    /// <summary>
    /// Gets or sets the minimum tile Y (row) value.
    /// </summary>
    [Column("tr_min")]
    public int YMin { get; set; }
    /// <summary>
    /// Gets or sets the maximum tile Y (row) value.
    /// </summary>
    [Column("tr_max")]
    public int YMax { get; set; }
    /// <summary>
    /// Gets or sets the minimum tile X (column) value.
    /// </summary>
    [Column("tc_min")]
    public int XMin { get; set; }
    /// <summary>
    /// Gets or sets the maximum tile X (column) value.
    /// </summary>
    [Column("tc_max")]
    public int XMax { get; set; }

    /// <summary>
    /// Creates a tile range from the minimum and maximum X and Y values.
    /// </summary>
    /// <param name="zoomLevel">Zoom level of the tile range.</param>
    /// <returns>The tile range for this zoom level.</returns>
    public TileRange ToTileRange(int zoomLevel)
    {
        return new TileRange(XMin, YMin, XMax, YMax, zoomLevel);
    }
}
