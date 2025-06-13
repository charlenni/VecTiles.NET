using VecTiles.Common.Primitives;
using VecTiles.Converters.Mapbox;
using VecTiles.DataSources.MbTiles;
using VecTiles.TileDataSources;

namespace VecTiles.Converters.Tests;

public class MapboxConverterTests
{
    readonly string _path = "files\\zurich.mbtiles";

    [Fact]
    public async Task VectorTileConverterTest()
    {
        var dataSource = new MbTilesTileDataSource(_path, determineZoomLevelsFromTilesTable: true, determineTileRangeFromTilesTable: true);
        var tileConverter = new MapboxTileConverter();
        var vectorDataSource = new VectorTileDataSource(dataSource, tileConverter);

        Assert.True(dataSource.Version == "3.6.1");

        var tile = new Tile(8580, 5738, 14);

        var vectorTile = await vectorDataSource.GetVectorTileAsync(tile);

        Assert.NotNull(vectorTile);
        Assert.True(vectorTile.TileId == 183498457);
        Assert.True(vectorTile.IsEmpty == false);
        Assert.True(vectorTile.Layers.Count == 12);
        Assert.True(vectorTile.Layers[0].Name == "water");
        Assert.True(vectorTile.Layers[0].Features.Count == 9);
        Assert.True(vectorTile.Layers[0].Features[0].Attributes.Count == 2);
        Assert.True(vectorTile.Layers[0].Features[0].Attributes.GetNames()[0] == "class");
        Assert.True(vectorTile.Layers[0].Features[0].Attributes.GetValues()[0].ToString() == "river");
    }
}
