using VecTiles.Styles.OpenMapTiles;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests for resolving relative source URLs of an OMTSource against the base
/// directory of the style file (OMTSource.ResolveRelativeUrls).
/// </summary>
public class OMTSourceResolveRelativeUrlTests
{
    /// <summary>
    /// A pmtiles source with an absolute file path is returned unchanged.
    /// </summary>
    [Fact]
    public void PmTilesAbsolutePathIsUnchanged()
    {
        var source = new OMTSource { Url = "pmtiles://file:///data/maps/zuerich.pmtiles" };

        source.ResolveRelativeUrls("/styles/protomaps");

        Assert.Equal("pmtiles://file:///data/maps/zuerich.pmtiles", source.Url);
    }

    /// <summary>
    /// A pmtiles source with a path relative to the base directory keeps the
    /// pmtiles scheme, so the data source factory can route the URL.
    /// </summary>
    [Fact]
    public void PmTilesRelativePathIsResolvedAgainstBaseDirectory()
    {
        var source = new OMTSource { Url = "pmtiles://file://zuerich.pmtiles" };

        source.ResolveRelativeUrls("/styles/protomaps");

        Assert.Equal("pmtiles:///styles/protomaps/zuerich.pmtiles", source.Url);
    }

    /// <summary>
    /// A pmtiles source with an http(s) URL is returned unchanged.
    /// </summary>
    [Fact]
    public void PmTilesHttpsUrlIsUnchanged()
    {
        var source = new OMTSource { Url = "pmtiles://https://latest.protomaps.com/v4.pmtiles" };

        source.ResolveRelativeUrls("/styles/protomaps");

        Assert.Equal("pmtiles://https://latest.protomaps.com/v4.pmtiles", source.Url);
    }

    /// <summary>
    /// An mbtiles source with a relative path is resolved against the base
    /// directory and keeps the mbtiles scheme.
    /// </summary>
    [Fact]
    public void MbTilesRelativePathIsResolvedAgainstBaseDirectory()
    {
        var source = new OMTSource { Url = "mbtiles://zuerich.mbtiles" };

        source.ResolveRelativeUrls("/styles/osm-liberty");

        Assert.Equal("mbtiles:///styles/osm-liberty/zuerich.mbtiles", source.Url);
    }

    /// <summary>
    /// An http(s) URL of a source is returned unchanged.
    /// </summary>
    [Fact]
    public void HttpUrlIsUnchanged()
    {
        var source = new OMTSource { Url = "https://tiles.example.com/{z}/{x}/{y}.pbf" };

        source.ResolveRelativeUrls("/styles/osm-liberty");

        Assert.Equal("https://tiles.example.com/{z}/{x}/{y}.pbf", source.Url);
    }

    /// <summary>
    /// Relative URLs in the Tiles list are resolved as well.
    /// </summary>
    [Fact]
    public void TilesUrlsAreResolved()
    {
        var source = new OMTSource { Tiles = ["mapsforge://berlin.map"] };

        source.ResolveRelativeUrls("/styles");

        Assert.Equal("mapsforge:///styles/berlin.map", source.Tiles[0]);
    }
}
