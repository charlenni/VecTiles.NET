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
    /// A scheme-less relative URL (e.g. a TileJSON reference next to the style
    /// file) is resolved against the base directory and gets the file scheme,
    /// because the data and byte sources only understand URLs with a scheme.
    /// </summary>
    [Fact]
    public void SchemelessRelativeUrlIsResolvedAgainstBaseDirectory()
    {
        var source = new Source { Url = "tiles.json" };

        source.ResolveRelativeUrls("/styles/protomaps");

        Assert.Equal("file:///styles/protomaps/tiles.json", source.Url);
    }

    /// <summary>
    /// A scheme-less absolute path gets the file scheme and is not combined
    /// with the base directory.
    /// </summary>
    [Fact]
    public void SchemelessAbsolutePathGetsFileScheme()
    {
        var source = new Source { Url = "/data/tiles.json" };

        source.ResolveRelativeUrls("/styles/protomaps");

        Assert.Equal("file:///data/tiles.json", source.Url);
    }

    /// <summary>
    /// A protocol-relative URL ("//host/path") is not a file path and is
    /// returned unchanged.
    /// </summary>
    [Fact]
    public void ProtocolRelativeUrlIsUnchanged()
    {
        var source = new Source { Url = "//tiles.example.com/tiles.json" };

        source.ResolveRelativeUrls("/styles/protomaps");

        Assert.Equal("//tiles.example.com/tiles.json", source.Url);
    }

    /// <summary>
    /// The tile URLs of a file based TileJSON are resolved against the folder
    /// of the TileJSON file, not the style directory: scheme-less entries get
    /// the file scheme, entries with a file based scheme keep it.
    /// </summary>
    [Fact]
    public void TileJsonTileUrlsAreResolvedAgainstTileJsonDirectory()
    {
        var resolved = Source.ResolveTileJsonTileUrls(
            ["zuerich.pmtiles", "mbtiles://zuerich.mbtiles", "https://tiles.example.com/{z}/{x}/{y}.pbf"],
            "file:///styles/maplibre/tiles.json");

        Assert.Equal(
            ["file:///styles/maplibre/zuerich.pmtiles", "mbtiles:///styles/maplibre/zuerich.mbtiles", "https://tiles.example.com/{z}/{x}/{y}.pbf"],
            resolved);
    }

    /// <summary>
    /// The tile URLs of a network TileJSON are absolute per specification and
    /// are left unchanged.
    /// </summary>
    [Fact]
    public void TileJsonTileUrlsOfNetworkTileJsonAreUnchanged()
    {
        var resolved = Source.ResolveTileJsonTileUrls(
            ["https://tiles.example.com/{z}/{x}/{y}.pbf"],
            "https://tiles.example.com/tiles.json");

        Assert.Equal(["https://tiles.example.com/{z}/{x}/{y}.pbf"], resolved);
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
