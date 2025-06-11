using SQLite;

namespace VecTiles.DataSources.MbTiles.Tables;

[Table("tiles")]
public class ZoomLevel // I would rather just use 'int' instead of this class in Query, but can't get it to work
{
    [Column("level")]
    public int Level { get; set; }
}
