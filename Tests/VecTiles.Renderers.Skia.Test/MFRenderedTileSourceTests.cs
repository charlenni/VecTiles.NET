using BruTile;
using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Converters.Mapsforge;
using VecTiles.Controls.Mapsui;
using Viewport = Mapsui.Viewport;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Smoke tests of <see cref="MFRenderedTileSource"/>: the Mapsforge map file of the repo is
/// rendered with a Mapsforge render theme and with an MapLibre style built for Mapsforge
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
            _mapFile, new IdentityTagsConverter("mapsforge"), _mapsforgeTheme);

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

    /// <summary>
    /// Draws the tile of the Mapsforge theme together with its 3x3 surroundings in the draw
    /// phase of the layer renderer (synchronous placement): this exercises the placement and
    /// drawing of path texts (street names along roads), which crashed with a
    /// NullReferenceException on the unset opacity of the path text symbol before the
    /// TextLineSymbol opacity default was fixed.
    /// </summary>
    [Fact]
    public void MapsforgeTheme_DrawPhasePlacesPathTextWithoutException()
    {
        const int tileSize = 512;
        const int size = 3 * tileSize;
        const double worldHalf = 20037508.343;

        double resolution = 78271.51696402031 / Math.Pow(2, Zoom); // initial resolution of a 512 tile in EPSG:3857
        double tileSpan = tileSize * resolution;

        double centerXWorld = -worldHalf + (TileX + 0.5) * tileSpan;
        double centerYWorld = worldHalf - (TileY + 0.5) * tileSpan;
        var viewport = new Viewport(centerXWorld, centerYWorld, resolution, 0, size, size);

        using var tileSource = new MFRenderedTileSource(
            _mapFile, new IdentityTagsConverter("mapsforge"), _mapsforgeTheme);
        var tileLayer = new RenderedTileLayer(tileSource) { SynchronousPlacement = true };
        var renderService = new Mapsui.Rendering.RenderService();

        using var bitmap = new SKBitmap(size, size);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Beige);

        RenderedTileLayerRenderer.Draw(canvas, viewport, tileLayer, renderService);
    }
}
