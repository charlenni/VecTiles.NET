namespace VecTiles.DataSources.PmTiles;

/// <summary>
/// Type of the tiles in a PMTiles archive
/// (see https://github.com/protomaps/PMTiles/blob/main/spec/v3/spec.md, chapter 3.2)
/// </summary>
public enum PmTilesTileType
{
    /// <summary>
    /// Unknown or other tile type
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Mapbox vector tile
    /// </summary>
    Mvt = 1,

    /// <summary>
    /// PNG image
    /// </summary>
    Png = 2,

    /// <summary>
    /// JPEG image
    /// </summary>
    Jpeg = 3,

    /// <summary>
    /// WebP image
    /// </summary>
    Webp = 4,

    /// <summary>
    /// AVIF image
    /// </summary>
    Avif = 5,

    /// <summary>
    /// MapLibre vector tile
    /// </summary>
    MapLibreVector = 6
}
