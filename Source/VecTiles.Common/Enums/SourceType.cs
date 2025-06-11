namespace VecTiles.Common.Enums;

/// <summary>
/// Represents the different types of data sources that can be used
/// </summary>
public enum SourceType
{
    /// <summary>
    /// Vector data source (e.g., vector tiles).
    /// </summary>
    Vector,

    /// <summary>
    /// Raster data source (e.g., raster tiles).
    /// </summary>
    Raster,

    /// <summary>
    /// Array of raster data sources.
    /// </summary>
    RasterArray,

    /// <summary>
    /// Raster Digital Elevation Model (DEM) data source.
    /// </summary>
    RasterDEM,

    /// <summary>
    /// GeoJSON data source.
    /// </summary>
    GeoJSON,

    /// <summary>
    /// Image data source.
    /// </summary>
    Image,

    /// <summary>
    /// Video data source.
    /// </summary>
    Video
}
