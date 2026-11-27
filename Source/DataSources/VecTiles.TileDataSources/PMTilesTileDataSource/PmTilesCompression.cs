namespace VecTiles.DataSources.PmTiles;

/// <summary>
/// Compression of the sections of a PMTiles archive
/// (see https://github.com/protomaps/PMTiles/blob/main/spec/v3/spec.md, chapter 3.3)
/// </summary>
public enum PmTilesCompression
{
    /// <summary>
    /// Unknown compression
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// No compression
    /// </summary>
    None = 1,

    /// <summary>
    /// gzip compression
    /// </summary>
    Gzip = 2,

    /// <summary>
    /// brotli compression
    /// </summary>
    Brotli = 3,

    /// <summary>
    /// zstd compression
    /// </summary>
    Zstd = 4
}
