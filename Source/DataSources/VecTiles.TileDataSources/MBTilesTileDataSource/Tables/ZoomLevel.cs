using SQLite;

namespace VecTiles.DataSources.MbTiles.Tables;

/// <summary>
/// Represents a zoom level row queried from the 'tiles' table of an MBTiles database.
/// </summary>
[Table("tiles")]
public class ZoomLevel // I would rather just use 'int' instead of this class in Query, but can't get it to work
{
    /// <summary>
    /// Gets or sets the zoom level.
    /// </summary>
    [Column("level")]
    public int Level { get; set; }
}
