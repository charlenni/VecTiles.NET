namespace VecTiles.DataSources.Mapsforge;

/// <summary>
/// Describes a zoom interval of a Mapsforge map file.
/// </summary>
/// <remarks>
/// Each zoom interval corresponds to one sub-file in the map file. All tile data of the zoom
/// levels of an interval is stored in the tile grid of the intervals base zoom level, in the
/// tile order of the index segment. A converter that interprets the tile bytes of a tile uses
/// the <see cref="BaseZoom"/> level to determine the tile the data belongs to.
/// </remarks>
/// <param name="BaseZoom">Base zoom of the sub-file; zoom level of the tile grid in which the tile data is stored</param>
/// <param name="MinZoom">Minimal zoom level covered by the sub-file</param>
/// <param name="MaxZoom">Maximal zoom level covered by the sub-file</param>
public sealed record MapsforgeSubFile(int BaseZoom, int MinZoom, int MaxZoom)
{
    /// <summary>
    /// Absolute start position of the sub-file within the map file in bytes.
    /// </summary>
    public long StartOffset { get; init; }

    /// <summary>
    /// Size of the complete sub-file (tile index segment and tile data segment) in bytes.
    /// </summary>
    public long Size { get; init; }
}
