using VecTiles.Common.Enums;
using HttpDataSource = VecTiles.DataSources.HttpTileDataSource.HttpTileDataSource;

namespace VecTiles.DataSources.Tests;

/// <summary>
/// Tests for the HttpTileDataSource construction: the zoom range of the
/// source must be exposed, because the overzoom fallback of the
/// VectorTileDataSource relies on it.
/// </summary>
public class HttpTileDataSourceTests
{
    /// <summary>
    /// Verifies that the constructor exposes the zoom range and source type of the template URL source.
    /// </summary>
    [Fact]
    public void Constructor_ExposesZoomRangeOfSource()
    {
        var source = new HttpDataSource(["https://example.org/{z}/{x}/{y}.pbf"], 5, 14);

        Assert.Equal(5, source.MinZoom);
        Assert.Equal(14, source.MaxZoom);
        Assert.Equal(SourceType.Vector, source.SourceType);
    }
}
