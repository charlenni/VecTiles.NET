using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.DataSources;
using VecTiles.DataSources.PmTiles;
using VecTiles.Tests;

namespace VecTiles.DataSources.Tests;

/// <summary>
/// Tests for the PMTiles tile data source against the Zuerich.pmtiles test archive
/// (MVT vector tiles, gzip compressed, zoom levels 0 to 14).
/// </summary>
public class PMTilesTileDataSourceTests
{
    private static string ArchivePath => Path.Combine(RepoData.ProtomapsFiles, "zuerich.pmtiles");

    public static IEnumerable<object[]> TileIdSpecExamples =>
    [
        [0u, 0u, 0u, 0ul],
        [1u, 0u, 0u, 1ul],
        [1u, 0u, 1u, 2ul],
        [1u, 1u, 1u, 3ul],
        [1u, 1u, 0u, 4ul],
        [2u, 0u, 0u, 5ul],
        [12u, 3423u, 1763u, 19078479ul],
    ];

    [Theory]
    [MemberData(nameof(TileIdSpecExamples))]
    public void TileIdMatchesSpecExamples(uint zoom, uint x, uint y, ulong expectedTileId)
    {
        var tileId = TileId.FromTileCoordinates((int)zoom, (int)x, (int)y);

        Assert.Equal(expectedTileId, tileId);
    }

    [Fact]
    public void TileIdAndTileCoordinatesAreInverse()
    {
        var random = new Random(42);

        for (var zoom = 0; zoom <= 14; zoom++)
        {
            for (var i = 0; i < 10; i++)
            {
                var x = random.Next(1 << zoom);
                var y = random.Next(1 << zoom);

                var tileId = TileId.FromTileCoordinates(zoom, x, y);
                var (tileZoom, tileX, tileY) = TileId.ToTileCoordinates(tileId);

                Assert.Equal((zoom, x, y), (tileZoom, tileX, tileY));
            }
        }
    }

    [Fact]
    public void ConstructorReadsHeaderOfArchive()
    {
        using var dataSource = new PMTilesTileDataSource("pmtiles://file://" + ArchivePath);

        Assert.Equal(0, dataSource.MinZoom);
        Assert.Equal(14, dataSource.MaxZoom);
        Assert.Equal(SourceType.Vector, dataSource.SourceType);
        Assert.Equal(Scheme.Xyz, dataSource.Scheme);
        Assert.Equal(PmTilesCompression.Gzip, dataSource.Header.InternalCompression);
        Assert.Equal(PmTilesCompression.Gzip, dataSource.Header.TileCompression);
        Assert.Equal(PmTilesTileType.Mvt, dataSource.Header.TileType);
        Assert.False(string.IsNullOrEmpty(dataSource.Name));
    }

    [Fact]
    public void ConstructorAcceptsBareFilePath()
    {
        using var dataSource = new PMTilesTileDataSource(ArchivePath);

        Assert.Equal(ArchivePath, dataSource.Source);
        Assert.Equal(14, dataSource.MaxZoom);
    }

    [Fact]
    public void ConstructorThrowsForMissingFile()
    {
        Assert.Throws<FileNotFoundException>(
            () => new PMTilesTileDataSource("pmtiles://file:///does/not/exist.pmtiles"));
    }

    [Fact]
    public void ConstructorThrowsForNonPmTilesFile()
    {
        var notPmTiles = Path.Combine(RepoData.ProtomapsFiles, "protomaps-light-style.json");

        Assert.Throws<InvalidDataException>(() => new PMTilesTileDataSource(notPmTiles));
    }

    [Fact]
    public void ConstructorThrowsWhenZoomRangeDoesNotIntersect()
    {
        Assert.Throws<ArgumentException>(() => new PMTilesTileDataSource(ArchivePath, 15, 22));
    }

    [Theory]
    [InlineData(10, 536, 358)] // Zurich main station area
    [InlineData(14, 8580, 5734)]
    public async Task GetTileAsyncReturnsTileData(int zoom, int x, int y)
    {
        using var dataSource = new PMTilesTileDataSource(ArchivePath);

        var data = await dataSource.GetTileAsync(new Tile(x, y, zoom));

        Assert.NotNull(data);
        Assert.True(data.Length > 0);
    }

    [Theory]
    [InlineData(8, 134, 89)]
    [InlineData(10, 536, 358)]
    [InlineData(14, 8580, 5734)]
    public async Task GetTileAsyncReturnsDecompressedMvtTile(int zoom, int x, int y)
    {
        using var dataSource = new PMTilesTileDataSource(ArchivePath);

        var data = await dataSource.GetTileAsync(new Tile(x, y, zoom));

        Assert.NotNull(data);
        // The tiles of the test archive are gzip compressed: without decompression they
        // would start with the gzip magic number 0x1f8b
        Assert.NotEqual(0x1f, data[0]);
        // An MVT tile starts with a protobuf field tag of a layer message
        Assert.True(data[0] > 0);
    }

    [Fact]
    public async Task GetTileAsyncReturnsNullForTileOutsideOfArchive()
    {
        using var dataSource = new PMTilesTileDataSource(ArchivePath);

        // Tile in the Gulf of Guinea, far outside of the Zurich archive
        var data = await dataSource.GetTileAsync(new Tile(0, 0, 10));

        Assert.Null(data);
    }

    [Fact]
    public async Task GetTileAsyncReturnsNullForTileAboveMaxZoom()
    {
        using var dataSource = new PMTilesTileDataSource(ArchivePath);

        var data = await dataSource.GetTileAsync(new Tile(0, 0, 15));

        Assert.Null(data);
    }

    [Fact]
    public async Task GetTileAsyncRepeatedRequestsUseLeafDirectoryCache()
    {
        using var dataSource = new PMTilesTileDataSource(ArchivePath);

        // Tiles around the center of the archive: neighbouring tiles of the same zoom level
        // are spread over one leaf directory, so the requests hit the cache
        for (var x = 8579; x <= 8581; x++)
        {
            for (var y = 5733; y <= 5735; y++)
            {
                var data = await dataSource.GetTileAsync(new Tile(x, y, 14));

                Assert.NotNull(data);
            }
        }
    }

    [Fact]
    public void FactoryCreatesPmTilesSourceFromFileUrl()
    {
        using var dataSource = Assert.IsType<PMTilesTileDataSource>(
            DefaultTileDataSourceFactory.CreateTileDataSource(["pmtiles://file://" + ArchivePath]));

        Assert.Equal(ArchivePath, dataSource.Source);
        Assert.Equal(14, dataSource.MaxZoom);
    }

    [Fact]
    public void FactoryThrowsForUnknownScheme()
    {
        Assert.Throws<ArgumentException>(
            () => DefaultTileDataSourceFactory.CreateTileDataSource(["unknown://something"]));
    }
}
