using BruTile;
using VecTiles.Converters.Mapsforge;
using VecTiles.Controls.Mapsui;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Smoke tests of <see cref="MFRenderedTileSource"/>: the Mapsforge map file of the repo is
/// rendered with a Mapsforge render theme and with an OpenMapTiles style built for Mapsforge
/// data, and the tiles served for a tile covering Zürich must contain rendered layers.
/// </summary>
public class MFRenderedTileSourceTests
{
    private static readonly string _mapFile = Path.Combine(Utilities.DataFiles, "zuerich.map");
    private static readonly string _mapsforgeTheme = Path.Combine(Utilities.DataFiles, "..",
        "Mapsforge", "mapsforge", "default.xml");
    private static readonly string _omtStyle = Path.Combine(
        Utilities.DataFiles, "osm-liberty-for-mapsforge.json");

    /// <summary>
    /// Tile (x, y) of Zürich at zoom 14, inside the bounding box of the map file.
    /// </summary>
    private const int Zoom = 14;
    private const int TileX = 8577;
    private const int TileY = 5736;

    [Fact]
    public async Task MapsforgeTheme_RendersTileWithLayers()
    {
        using var tileSource = new MFRenderedTileSource(
            _mapFile, MapsforgeIdentityTagsConverter.Instance, _mapsforgeTheme);

        Assert.Equal("default", tileSource.Name);
        Assert.NotEmpty(tileSource.Styles);

        var tile = await tileSource.GetTileAsync(new TileInfo
        {
            Index = new TileIndex(TileX, TileY, Zoom),
        });

        Assert.NotNull(tile);
        Assert.NotEmpty(tile!.Layers);
    }

    [Fact]
    public async Task OpenMapTilesStyle_RendersTileWithLayers()
    {
        using var tileSource = new MFRenderedTileSource(_mapFile, null, _omtStyle);

        Assert.NotEmpty(tileSource.Styles);

        var tile = await tileSource.GetTileAsync(new TileInfo
        {
            Index = new TileIndex(TileX, TileY, Zoom),
        });

        Assert.NotNull(tile);
        Assert.NotEmpty(tile!.Layers);
    }
}
