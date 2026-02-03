using VecTiles.Common.Primitives;
using VecTiles.Converters.OpenMapTiles;
using VecTiles.DataSources.MbTiles;
using VecTiles.TileDataSources;

namespace VecTiles.Converters.Tests;

public class OMTConverterTests
{
    readonly string _path = "files\\zurich.mbtiles";

    [Fact]
    public async Task VectorTileConverterTest()
    {
        var dataSource = new MbTilesTileDataSource(_path, determineZoomLevelsFromTilesTable: true, determineTileRangeFromTilesTable: true);
        var tileConverter = new OMTTileConverter();
        var vectorDataSource = new VectorTileDataSource(dataSource, tileConverter);

        Assert.True(dataSource.Version == "3.15.0");

        var tile = new Tile(8580, 5738, 14);

        var vectorTile = await vectorDataSource.GetVectorTileAsync(tile);

        Assert.NotNull(vectorTile);
        Assert.True(vectorTile.TileId == 183498457);
        Assert.True(vectorTile.IsEmpty == false);
        Assert.True(vectorTile.Layers.Count == 13);
        Assert.True(vectorTile.Layers[10].Name == "water");
        Assert.True(vectorTile.Layers[10].Features.Count == 24);
        Assert.True(vectorTile.Layers[10].Features[0].Attributes.Count == 3);
        Assert.True(vectorTile.Layers[10].Features[0].Attributes.GetNames()[1] == "class");
        Assert.True(vectorTile.Layers[10].Features[0].Attributes.GetValues()[1].ToString() == "lake");
    }
}
