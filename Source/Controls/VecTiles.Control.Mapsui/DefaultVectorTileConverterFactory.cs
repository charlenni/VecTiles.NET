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
            : dataSource is MbTilesTileDataSource { Format: MbTilesFormat.Mlt }
                || dataSource is PmTilesTileDataSource { Header.TileType: PmTilesTileType.MapLibreVector }
                || IsMltHttpSource(dataSource)
                ? new MltTileConverter(IdentityTagsConverter.Instance)
                : new MVTTileConverter(IdentityTagsConverter.Instance);

    /// <summary>
    /// HTTP sources declare their tile format either in the 'encoding' entry
    /// of their TileJSON (the reliable place; HTTP response headers carry no
    /// tile format) or in the file extension of their URL template,
    /// e.g. the '.mlt' tiles of the MapLibre demotiles TileJSON.
    /// </summary>
    private static bool IsMltHttpSource(ITileDataSource dataSource) =>
        dataSource is HttpTileDataSource httpSource
        && (string.Equals(httpSource.TileEncoding, "mlt", StringComparison.OrdinalIgnoreCase)
            || httpSource.Sources.Any(s => s.EndsWith(".mlt", StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Gets the tile converter factory of the vector sources of a style file.
    /// </summary>
    public static Func<ITileDataSource, ITileConverter>? Factory => ForDataSource;
}
