using VecTiles.Styles.MapLibre;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests for resolving relative source URLs of an Source against the base
/// directory of the style file (Source.ResolveRelativeUrls).
/// </summary>
public class SourceResolveRelativeUrlTests
{
    /// <summary>
    /// A pmtiles source with an absolute file path is returned unchanged.
    /// </summary>
    [Fact]
    public void PmTilesAbsolutePathIsUnchanged()
    {
        var source = new Source { Url = "pmtiles://file:///data/maps/zuerich.pmtiles" };

        source.ResolveRelativeUrls("/styles/protomaps");

        Assert.Equal("pmtiles://file:///data/maps/zuerich.pmtiles", source.Url);
    }

    /// <summary>
    /// A pmtiles source with a path relative to the base directory keeps the
    /// pmtiles scheme, so the data source factory can route the URL. The path
    /// is only resolved when the resolved file exists.
    /// </summary>
    [Fact]
    public void PmTilesRelativePathIsResolvedAgainstBaseDirectory()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), "vectiles-omtsource-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDirectory);
        try
        {
            File.WriteAllText(Path.Combine(baseDirectory, "zuerich.pmtiles"), string.Empty);

            var source = new Source { Url = "pmtiles://file://zuerich.pmtiles" };

            source.ResolveRelativeUrls(baseDirectory);

            Assert.Equal($"pmtiles://{Path.Combine(baseDirectory, "zuerich.pmtiles")}", source.Url);
        }
        finally
        {
            Directory.Delete(baseDirectory, true);
        }
    }

    /// <summary>
    /// A pmtiles source whose file exists neither relative to the working
    /// directory nor relative to the base directory is returned unchanged.
    /// </summary>
    [Fact]
    public void PmTilesMissingFileIsUnchanged()
    {
        var source = new Source { Url = "pmtiles://file://missing.pmtiles" };

        source.ResolveRelativeUrls("/nonexistent/directory");

        Assert.Equal("pmtiles://file://missing.pmtiles", source.Url);
    }

    /// <summary>
    /// A pmtiles source with an http(s) URL is returned unchanged.
    /// </summary>
    [Fact]
    public void PmTilesHttpsUrlIsUnchanged()
    {
        var source = new Source { Url = "pmtiles://https://latest.protomaps.com/v4.pmtiles" };

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
        var source = new Source { Url = "mbtiles://zuerich.mbtiles" };

        source.ResolveRelativeUrls("/styles/osm-liberty");

        Assert.Equal("mbtiles:///styles/osm-liberty/zuerich.mbtiles", source.Url);
    }

    /// <summary>
    /// An http(s) URL of a source is returned unchanged.
    /// </summary>
    [Fact]
    public void HttpUrlIsUnchanged()
    {
        var source = new Source { Url = "https://tiles.example.com/{z}/{x}/{y}.pbf" };

        source.ResolveRelativeUrls("/styles/osm-liberty");

        Assert.Equal("https://tiles.example.com/{z}/{x}/{y}.pbf", source.Url);
    }

    /// <summary>
    /// Relative URLs in the Tiles list are resolved as well.
    /// </summary>
    [Fact]
    public void TilesUrlsAreResolved()
    {
        var source = new Source { Tiles = ["mapsforge://berlin.map"] };

        source.ResolveRelativeUrls("/styles");

        Assert.Equal("mapsforge:///styles/berlin.map", source.Tiles[0]);
    }
}
