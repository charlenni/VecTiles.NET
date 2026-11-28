using VecTiles.Common.Interfaces;
using VecTiles.Converters.MLT;
using VecTiles.Converters.Mapsforge;
using VecTiles.Converters.MVT;
using VecTiles.DataSources.HttpTileDataSource;
using VecTiles.DataSources.Mapsforge;
using VecTiles.DataSources.MbTiles;
using VecTiles.DataSources.PmTiles;

namespace VecTiles.Controls.Mapsui;

/// <summary>
/// Default factory for the tile converter of the vector sources of a style
/// file (see <see cref="Source.VectorTileConverterFactory"/>): the tiles
/// of a mapsforge file are raw mapsforge data, their source layers and tags
/// follow the MapLibre schema only after the conversion of the mapsforge
/// tile converter; MapLibre data is already organized in the source
/// layers and tags of the style, so its converter gets the (strict) identity
/// tags converter.
/// </summary>
internal static class DefaultVectorTileConverterFactory
{
    private static readonly Func<ITileDataSource, ITileConverter> ForDataSource = dataSource =>
        dataSource is MapsforgeTileDataSource
            ? new MapsforgeTileConverter()
            : UsesMltEncoding(dataSource)
                ? new MltTileConverter(IdentityTagsConverter.Instance)
                : new MVTTileConverter(IdentityTagsConverter.Instance);

    /// <summary>
    /// Gets the tile format of a vector source: an explicit 'encoding' entry
    /// (the 'encoding' of the style source or of the TileJSON of an HTTP
    /// source) wins over the native format declarations of the data sources
    /// (MBTiles metadata format, PMTiles header tile type). Without any
    /// declaration the source is MVT, per style specification default.
    /// </summary>
    private static bool UsesMltEncoding(ITileDataSource dataSource)
    {
        var explicitEncoding = dataSource switch
        {
            HttpTileDataSource httpSource => httpSource.TileEncoding,
            PmTilesTileDataSource { TileEncoding: { } tileEncoding } => tileEncoding,
            _ => null,
        };

        if (!string.IsNullOrWhiteSpace(explicitEncoding))
            return explicitEncoding.Equals("mlt", StringComparison.OrdinalIgnoreCase)
                || explicitEncoding.Equals("maplibre", StringComparison.OrdinalIgnoreCase);

        return dataSource switch
        {
            MbTilesTileDataSource { Format: MbTilesFormat.Mlt } => true,
            PmTilesTileDataSource { Header.TileType: PmTilesTileType.MapLibreVector } => true,
            // HTTP sources have no format entry of their own; the file
            // extension of the tile template is the only hint, e.g. the
            // '.mlt' tiles of the MapLibre demotiles
            HttpTileDataSource httpSource => httpSource.Sources.Any(
                s => s.EndsWith(".mlt", StringComparison.OrdinalIgnoreCase)),
            _ => false,
        };
    }

    /// <summary>
    /// Gets the tile converter factory of the vector sources of a style file.
    /// </summary>
    public static Func<ITileDataSource, ITileConverter>? Factory => ForDataSource;
}
