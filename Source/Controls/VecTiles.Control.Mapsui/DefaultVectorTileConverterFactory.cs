using VecTiles.Common.Interfaces;
using VecTiles.Converters.Mapsforge;
using VecTiles.Converters.MVT;
using VecTiles.DataSources.Mapsforge;

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
            : new MVTTileConverter(IdentityTagsConverter.Instance);

    /// <summary>
    /// Gets the tile converter factory of the vector sources of a style file.
    /// </summary>
    public static Func<ITileDataSource, ITileConverter>? Factory => ForDataSource;
}
