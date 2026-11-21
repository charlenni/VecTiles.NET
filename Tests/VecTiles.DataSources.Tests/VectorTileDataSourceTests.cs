using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.DataSources;
using VecTiles.TileDataSources;

namespace VecTiles.DataSources.Tests;

/// <summary>
/// Tests for the overzoom handling of the VectorTileDataSource: above the max
/// zoom of the underlying source the data is fetched directly from the
/// ancestor tile at the source's max zoom.
/// </summary>
public class VectorTileDataSourceTests
{
    /// <summary>
    /// A fake tile data source that records all requested tiles and answers
    /// via the <see cref="Respond"/> callback.
    /// </summary>
    private sealed class FakeTileDataSource(int maxZoom) : ITileDataSource
    {
        public List<Tile> Requests { get; } = [];

        public Func<Tile, byte[]?> Respond { get; set; } = _ => null;

        public string Name => "fake";

        public string Attribution => "fake";

        public int MinZoom => 0;

        public int MaxZoom { get; } = maxZoom;

        public SourceType SourceType => SourceType.Vector;

        public Scheme Scheme => Scheme.Xyz;

        public Task<byte[]?> GetTileAsync(Tile tile)
        {
            Requests.Add(tile);
            return Task.FromResult(Respond(tile));
        }
    }

    /// <summary>
    /// A fake converter that records the last Convert call.
    /// </summary>
    private sealed class FakeConverter : ITileConverter
    {
        public (Tile Requested, Tile Provided, byte[] Data)? LastCall { get; private set; }

        public Task<VectorTile?> Convert(Tile requestedTile, Tile providedTile, byte[] data)
        {
            LastCall = (requestedTile, providedTile, data);
            return Task.FromResult<VectorTile?>(new VectorTile());
        }
    }

    [Fact]
    public async Task OverzoomTile_FetchesAncestorAtSourceMaxZoomDirectly()
    {
        var dataSource = new FakeTileDataSource(maxZoom: 14)
        {
            Respond = tile => tile.Zoom == 14 ? [1, 2, 3] : null,
        };
        var converter = new FakeConverter();
        var vectorDataSource = new VectorTileDataSource(dataSource, converter);

        // Tile at Z16: the source only provides Z14, so the Z14 ancestor
        // (both coordinates divided by 4) must be requested directly, without
        // failing requests for Z16 and Z15
        var requested = new Tile(8765, 5678, 16);
        var result = await vectorDataSource.GetVectorTileAsync(requested);

        Assert.NotNull(result);
        var provided = Assert.Single(dataSource.Requests);
        Assert.Equal(14, provided.Zoom);
        Assert.Equal(2191, provided.X);
        Assert.Equal(1419, provided.Y);
        Assert.Equal(requested, converter.LastCall!.Value.Requested);
        Assert.Equal(provided, converter.LastCall!.Value.Provided);
        Assert.Equal([1, 2, 3], converter.LastCall!.Value.Data);
    }

    [Fact]
    public async Task TileWithinZoomRange_FetchesRequestedTileOnly()
    {
        var dataSource = new FakeTileDataSource(maxZoom: 14)
        {
            Respond = tile => tile.Zoom == 12 ? [7] : null,
        };
        var converter = new FakeConverter();
        var vectorDataSource = new VectorTileDataSource(dataSource, converter);

        var requested = new Tile(2147, 1430, 12);
        var result = await vectorDataSource.GetVectorTileAsync(requested);

        Assert.NotNull(result);
        var fetched = Assert.Single(dataSource.Requests);
        Assert.Equal(requested, fetched);
        Assert.Equal(requested, converter.LastCall!.Value.Requested);
        Assert.Equal(requested, converter.LastCall!.Value.Provided);
    }

    [Fact]
    public async Task MissingTileWithinZoomRange_FallsBackToParent()
    {
        var dataSource = new FakeTileDataSource(maxZoom: 14)
        {
            // Sparse source: Z13 has no data, the parent at Z12 does
            Respond = tile => tile.Zoom == 12 ? [7] : null,
        };
        var converter = new FakeConverter();
        var vectorDataSource = new VectorTileDataSource(dataSource, converter);

        var requested = new Tile(4294, 2860, 13);
        var result = await vectorDataSource.GetVectorTileAsync(requested);

        Assert.NotNull(result);
        Assert.Equal(2, dataSource.Requests.Count);
        Assert.Equal(13, dataSource.Requests[0].Zoom);
        Assert.Equal(12, dataSource.Requests[1].Zoom);
        Assert.Equal(requested, converter.LastCall!.Value.Requested);
        Assert.Equal(12, converter.LastCall!.Value.Provided.Zoom);
    }

    [Fact]
    public async Task NoDataAnywhere_ClampsFirstRequestAndWalksDownToRoot()
    {
        var dataSource = new FakeTileDataSource(maxZoom: 14);
        var converter = new FakeConverter();
        var vectorDataSource = new VectorTileDataSource(dataSource, converter);

        // Z17 request: the first fetch goes directly to the Z14 ancestor,
        // the fallback then walks down to the root tile without finding data
        var result = await vectorDataSource.GetVectorTileAsync(new Tile(100, 100, 17));

        Assert.Null(result);
        Assert.Null(converter.LastCall);
        // 1 request at Z14 (clamped) plus the parents Z13..Z0
        Assert.Equal(15, dataSource.Requests.Count);
        Assert.Equal(14, dataSource.Requests[0].Zoom);
        Assert.Equal(0, dataSource.Requests[^1].Zoom);
    }
}
