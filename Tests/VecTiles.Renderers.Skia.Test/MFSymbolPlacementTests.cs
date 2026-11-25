using BruTile;
using SkiaSharp;
using VecTiles.Converters.Mapsforge;
using VecTiles.Common.Interfaces;
using VecTiles.Controls.Mapsui;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia;
using Viewport = Mapsui.Viewport;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Regression tests of the Mapsforge symbol pipeline (icons, captions and
/// path texts). Guards against the two defects found when symbols did not
/// show at all:
/// - the symbol factory must wrap its point symbols in a PointSymbol
///   envelope (bare TextPointSymbol / IconPointSymbol objects are quietly
///   skipped by the placement engine and the draw paths), and
/// - the symbol geometries must be converted from the tile pixel space of
///   the mapsforge vector data into world coordinates (otherwise every
///   symbol is placed far outside the screen).
/// The tests render the Quaibruecke area of zuerich.map at zoom 16 with the
/// Mapsforge default theme.
/// </summary>
public class MFSymbolPlacementTests
{
    private static readonly string _mapFile = Path.Combine(Utilities.DataFiles, "..",
        "Mapsforge", "zuerich.map");
    private static readonly string _mapsforgeTheme = Path.Combine(Utilities.DataFiles, "..",
        "Mapsforge", "mapsforge", "default.xml");

    private static async Task<IRenderedTile> BakeQuaibrueckeTile()
    {
        using var tileSource = new MFRenderedTileSource(_mapFile, new IdentityTagsConverter("mapsforge"), _mapsforgeTheme);

        var tile = await tileSource.GetTileAsync(new TileInfo
        {
            Index = new TileIndex(34323, 22951, 16),
        });

        Assert.NotNull(tile);
        return tile!;
    }

    [Fact]
    public async Task BakedPointSymbolsAreWrappedInPointSymbolsTest()
    {
        var tile = await BakeQuaibrueckeTile();

        var pointSymbols = new List<VecTiles.Common.Primitives.PointSymbol>();
        foreach (var layer in tile.Layers.OfType<RenderedSymbolLayer>())
        {
            foreach (var symbol in layer.Symbols.Symbols)
            {
                if (symbol is VecTiles.Common.Primitives.TextPointSymbol or VecTiles.Common.Primitives.IconPointSymbol)
                {
                    Assert.Fail($"Point symbol of layer '{layer.Name}' is a bare " +
                                $"{symbol.GetType().Name}: the placement engine and the draw paths " +
                                "only handle PointSymbol envelopes, so the symbol would never show");
                }

                if (symbol is VecTiles.Common.Primitives.PointSymbol pointSymbol)
                {
                    pointSymbols.Add(pointSymbol);
                }
            }
        }

        // The test tile carries captions (place/street labels) and icons
        Assert.Contains(pointSymbols, s => s.TextSymbol is not null);
        Assert.Contains(pointSymbols, s => s.IconSymbol is not null);
    }

    [Fact]
    public async Task BakedPointSymbolsCarryWorldCoordinatesTest()
    {
        const int zoom = 16;
        const int TileX = 34323;
        const int TileY = 22951;

        var tile = await BakeQuaibrueckeTile();

        foreach (var layer in tile.Layers.OfType<RenderedSymbolLayer>())
        {
            foreach (var symbol in layer.Symbols.Symbols.OfType<VecTiles.Common.Primitives.PointSymbol>())
            {
                var geometry = symbol.Point;

                Assert.True(!double.IsNaN(geometry.X) && !double.IsNaN(geometry.Y) &&
                            !double.IsInfinity(geometry.X) && !double.IsInfinity(geometry.Y),
                            $"Point symbol of layer '{layer.Name}' has a non finite position");

                var west = WebMercatorX(TileX, zoom);
                var east = WebMercatorX(TileX + 1, zoom);
                var north = WebMercatorY(TileY, zoom);
                var south = WebMercatorY(TileY + 1, zoom);

                // Small pad: anchors and dy/dx offsets may move a point
                // symbol slightly across the tile boundary. The defect this
                // test guards against put the points millions of world units
                // away (tile pixels interpreted as world coordinates).
                var pad = (east - west) / 4;

                Assert.InRange(geometry.X, west - pad, east + pad);
                Assert.InRange(geometry.Y, south - pad, north + pad);
            }
        }
    }

    [Fact]
    public async Task PlatzpromenadeCaptionsArePlacedOnceAtAFixedPositionTest()
    {
        const int zoom = 18;
        const int tileSize = 512;
        // The Platzpromenade paths of zuerich.span reach the tiles
        // x = 137289..137291, y = 91792..91795; without the fixed way anchors
        // the caption of every way copy was placed per tile (duplicated,
        // position jumping between the tile copies)
        const int minX = 137289;
        const int minY = 91792;
        const int columns = 3;
        const int rows = 4;

        double resolution = 78271.51696402031 / Math.Pow(2, zoom);
        double tileSpan = tileSize * resolution;

        double centerXWorld = WebMercatorX(minX, zoom) + columns * tileSpan / 2.0;
        double centerYWorld = WebMercatorY(minY, zoom) - rows * tileSpan / 2.0;
        var viewport = new Viewport(centerXWorld, centerYWorld, resolution, 0, columns * tileSize, rows * tileSize);

        using var tileSource = new MFRenderedTileSource(_mapFile, new IdentityTagsConverter("mapsforge"), _mapsforgeTheme);
        var tileLayer = new RenderedTileLayer(tileSource) { SynchronousPlacement = true };
        var renderService = new Mapsui.Rendering.RenderService();

        using var bitmap = new SKBitmap(columns * tileSize, rows * tileSize);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        RenderedTileLayerRenderer.Draw(canvas, viewport, tileLayer, renderService);
        RenderedTileLayerRenderer.Draw(canvas, viewport, tileLayer, renderService);

        var view = tileLayer.CurrentPlacement;
        Assert.NotNull(view);
        Assert.False(view!.Placement.Incomplete);

        var placed = view.Placement.PlacedByLayer.Values
            .SelectMany(e => e)
            .Select(e => e.Symbol)
            .OfType<VecTiles.Common.Primitives.PointSymbol>()
            .Where(s => s.TextSymbol?.Text == "Platzpromenade")
            .Select(s => (s.Point.X, s.Point.Y))
            .ToList();

        // The thematic data of the plaza carries one caption symbol (on the
        // park polygon; the footways get no caption). In the mapsforge
        // priority placement the caption wins against the bank icon and is
        // placed exactly once, at the fixed world position of its anchor
        Assert.Equal(1, placed.Count);
        Assert.InRange(placed[0].Item1, 950670.2324864075 - 1e-3, 950670.2324864075 + 1e-3);
        Assert.InRange(placed[0].Item2, 6004461.071088876 - 1e-3, 6004461.071088876 + 1e-3);
    }

    private static double WebMercatorX(int tileX, int zoom) => -20037508.343 + (20037508.343 * 2) * tileX / Math.Pow(2, zoom);

    private static double WebMercatorY(int tileY, int zoom) => 20037508.343 - (20037508.343 * 2) * tileY / Math.Pow(2, zoom);

    [Fact]
    public async Task PlacementPlacesCaptionsAndPathTextsTest()
    {
        const int zoom = 16;
        const int tileSize = 512;
        const int TileX = 34323; // Zuerich, Buerkliplatz / Quaibruecke
        const int TileY = 22951;

        double resolution = 78271.51696402031 / Math.Pow(2, zoom);
        double tileSpan = tileSize * resolution;

        double centerXWorld = -20037508.343 + (TileX + 0.5) * tileSpan;
        double centerYWorld = 20037508.343 - (TileY + 0.5) * tileSpan;
        var viewport = new Viewport(centerXWorld, centerYWorld, resolution, 0, 3 * tileSize, 3 * tileSize);

        using var tileSource = new MFRenderedTileSource(_mapFile, new IdentityTagsConverter("mapsforge"), _mapsforgeTheme);
        var tileLayer = new RenderedTileLayer(tileSource) { SynchronousPlacement = true };
        var renderService = new Mapsui.Rendering.RenderService();

        using var bitmap = new SKBitmap(3 * tileSize, 3 * tileSize);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        RenderedTileLayerRenderer.Draw(canvas, viewport, tileLayer, renderService);
        RenderedTileLayerRenderer.Draw(canvas, viewport, tileLayer, renderService);

        var view = tileLayer.CurrentPlacement;
        Assert.NotNull(view);
        Assert.False(view!.Placement.Incomplete);

        var placedPointSymbols = view.Placement.PlacedByLayer.Values
            .SelectMany(e => e)
            .Select(e => e.Symbol)
            .OfType<VecTiles.Common.Primitives.PointSymbol>()
            .ToList();

        // Captions of the theme are placed (their predecessor defects: bare
        // TextPointSymbols never placed, tile pixel coordinates far off-screen)
        Assert.NotEmpty(placedPointSymbols.Where(s => s.TextSymbol is not null && s.DrawText));

        // Path texts (street labels along the line) are placed
        var placedRuns = view.Placement.PlacedByLayer.Values
            .SelectMany(e => e)
            .Where(e => e.Symbol is VecTiles.Common.Primitives.LineSymbol && e.Run is { HasPlacements: true });
        Assert.NotEmpty(placedRuns);
    }

    [Fact]
    public async Task PlacedCaptionIsVisibleOnAProbeCanvasTest()
    {
        const int zoom = 16;
        const int tileSize = 512;
        const int TileX = 34323;
        const int TileY = 22951;

        double resolution = 78271.51696402031 / Math.Pow(2, zoom);
        double tileSpan = tileSize * resolution;

        double centerXWorld = -20037508.343 + (TileX + 0.5) * tileSpan;
        double centerYWorld = 20037508.343 - (TileY + 0.5) * tileSpan;
        var viewport = new Viewport(centerXWorld, centerYWorld, resolution, 0, 3 * tileSize, 3 * tileSize);

        using var tileSource = new MFRenderedTileSource(_mapFile, new IdentityTagsConverter("mapsforge"), _mapsforgeTheme);
        var tileLayer = new RenderedTileLayer(tileSource) { SynchronousPlacement = true };
        var renderService = new Mapsui.Rendering.RenderService();

        using var bitmap = new SKBitmap(3 * tileSize, 3 * tileSize);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        RenderedTileLayerRenderer.Draw(canvas, viewport, tileLayer, renderService);
        RenderedTileLayerRenderer.Draw(canvas, viewport, tileLayer, renderService);

        var view = tileLayer.CurrentPlacement;
        Assert.NotNull(view);

        var placedText = view!.Placement.PlacedByLayer.Values
            .SelectMany(e => e)
            .Select(e => e.Symbol)
            .OfType<VecTiles.Common.Primitives.PointSymbol>()
            .Select(s => s.TextSymbol)
            .FirstOrDefault(t => t is not null);

        Assert.NotNull(placedText);

        // Draw the placed caption on a plain probe canvas: a caption whose
        // glyphs are measured and drawn (fonts, layout, halo) leaves ink
        // behind; a broken draw (missing typeface, NaN transforms) leaves the
        // canvas untouched
        using var probeBitmap = new SKBitmap(512, 256);
        using var probeCanvas = new SKCanvas(probeBitmap);
        probeCanvas.Clear(SKColors.White);

        TextPointSymbolRenderer.DrawText(probeCanvas,
            new VecTiles.Common.Primitives.EvaluationContext((float)zoom), placedText!, 256, 128, rotation: 0);
        probeCanvas.Flush();

        var ink = 0;
        foreach (var pixel in probeBitmap.Pixels)
        {
            if (pixel.Red < 200 || pixel.Green < 200 || pixel.Blue < 200)
            {
                ink++;
            }
        }

        Assert.True(ink > 50, $"The drawn caption left no ink on the probe canvas ({ink} non-white pixels)");
    }
}
