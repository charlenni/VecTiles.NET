using VecTiles.Common.Interfaces;
using VecTiles.DataSources.MbTiles;

namespace VecTiles.DataSources;

/// <summary>
/// This class provides a factory for creating ITileDataSource instances based 
/// on the scheme of the provided source URIs. 
/// Supported schemes (up to now): mbtiles, http, https, mapsforge, pmtiles.
/// Throws an exception for unknown schemes.
/// </summary>
public static class DefaultTileDataSourceFactory
{
    /// <summary>
    /// Scheme of a source URI that identifies a file in the file system.
    /// </summary>
    public const string FileScheme = "file";
    /// <summary>
    /// Scheme of a source URI that identifies an MBTiles file.
    /// </summary>
    public const string MbTilesScheme = "mbtiles";
    /// <summary>
    /// Scheme of a source URI that identifies tiles on an unsecured HTTP server.
    /// </summary>
    public const string HttpScheme = "http";
    /// <summary>
    /// Scheme of a source URI that identifies tiles on an HTTPS server.
    /// </summary>
    public const string HttpsScheme = "https";
    /// <summary>
    /// Scheme of a source URI that identifies tiles in a Mapsforge map file.
    /// </summary>
    public const string MapsforgeScheme = "mapsforge";
    /// <summary>
    /// Scheme of a source URI that identifies a PMTiles archive.
    /// </summary>
    public const string PmTilesScheme = "pmtiles";

    /// <summary>
    /// Create a ITileDataSource from provided source list
    /// </summary>
    /// <param name="sources">List of possible sources</param>
    /// <param name="minZoom">Minimum zoom of the source, 0 when unknown</param>
    /// <param name="maxZoom">Maximum zoom of the source, 22 when unknown</param>
    /// <returns>A ITileDataSource when one could created. If not, returns null.</returns>
    /// <exception cref="ArgumentException">Thrown, when source has unknown scheme</exception>
    public static ITileDataSource? CreateTileDataSource(string[] sources, int minZoom = 0, int maxZoom = 22)
    {
        if (sources == null || sources.Length == 0)
            return null;

        // Allways use the first one, even if there are more than one
        var source = sources[0];

        // Uri has a limitation of ~2000 bytes for URLs, so extract the scheme by hand
        var scheme = source.Substring(0, source.IndexOf(':'));

        return scheme switch
        {
            MbTilesScheme => CreateMbTilesSource(source.Substring(MbTilesScheme.Length + 3)),
            HttpScheme or HttpsScheme => CreateHttpSource(sources, minZoom, maxZoom),
            MapsforgeScheme => CreateMapsforgeSource(sources, minZoom, maxZoom),
            PmTilesScheme => CreatePmTilesSource(source),
            _ => throw new ArgumentException($"Scheme '{scheme}' of '{source}' is not supported"),
        };
    }

    private static ITileDataSource CreateMbTilesSource(string source)
    {
        return new MbTilesTileDataSource(source);
    }

    private static ITileDataSource CreateHttpSource(string[] sources, int minZoom, int maxZoom)
    {
        return new HttpTileDataSource.HttpTileDataSource(sources, minZoom, maxZoom);
    }
    
    private static ITileDataSource CreateMapsforgeSource(string[] sources, int minZoom, int maxZoom)
    {
        return new Mapsforge.MapsforgeTileDataSource(sources, minZoom, maxZoom);
    }

    private static ITileDataSource CreatePmTilesSource(string source)
    {
        return new PmTiles.PMTilesTileDataSource(source);
    }
}
