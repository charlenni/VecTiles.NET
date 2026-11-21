using BruTile;
using Mapsui.Extensions;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Skia;
using Xunit.Abstractions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Diagnostic: reproduces the user report of tile 17/68642/45902 — the
/// Schanzengraben water_name_line label is placed differently depending on
/// whether the neighbouring tile 68644 is part of the tile set.
/// Modes:
/// without = 1024x768 viewport centered on tile 68642 (68644 not on screen)
/// with = same viewport, but tile 68644 is added to the tile list artificially
/// panned = viewport shifted half a tile right, so 68644 is really on screen
/// </summary>
public class SchanzengrabenCrossTileTests
{
    private static readonly string _path = Utilities.DataFiles;

    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes the test with the xUnit output helper.
    /// </summary>
    /// <param name="output">The xUnit output helper.</param>
    public SchanzengrabenCrossTileTests(ITestOutputHelper output) => _output = output;

    private const int Zoom = 17;
    private const double WorldWidth = 40075016.686;
    private const double WorldHalf = WorldWidth / 2;
    private static double TileSpanWorld => WorldWidth / Math.Pow(2, Zoom);

    /// <summary>
    /// Renders the water_name_line placement variants around tile 17/68642/45902 and
    /// writes the result bitmaps for comparison of the label placement.
    /// </summary>
    /// <param name="mode">
    /// The variant: "without-68644", "with-68644" (tile added artificially) or "panned-right".
    /// </param>
    [Theory]
    [InlineData("without-68644")]
    [InlineData("with-68644")]
    [InlineData("panned-right")]
    public void RenderCrossTileVariants(string mode)
    {
        var tileSource = new OMTRenderedTileSource($"file://{_path}/osm-liberty.json");
        var service = new PlacementService(tileSource) { SynchronousPlacement = true };

        double resolution = Zoom.ToResolution();
        const int width = 1024, height = 768;

        // Base viewport: centered on the middle of tile 17/68642/45902
        double centerX = -WorldHalf + (68642 + 0.5) * TileSpanWorld;
        double centerY = WorldHalf - (45902 + 0.75) * TileSpanWorld;

        if (mode == "panned-right")
        {
            // Shift right by half a tile, so tile 68644 is really on screen
            centerX += TileSpanWorld / 2;
        }

        var viewport = new Mapsui.Viewport(centerX, centerY, resolution, 0, width, height);

        var tileInfos = tileSource.Schema.GetTileInfos(RendererExtent(viewport, tileSource.Schema), resolution)
            .ToArray();

        if (mode == "with-68644")
        {
            // Add the tile that is just off screen in the base viewport
            var schema = tileSource.Schema;
            var tileInfosWithNeighbor = new List<TileInfo>(tileInfos);

            var neighborInfo = schema.GetTileInfos(
                new BruTile.Extent(centerX + TileSpanWorld * 0.75, centerY - TileSpanWorld / 2,
                    centerX + TileSpanWorld * 1.25, centerY + TileSpanWorld / 2), resolution)
                .FirstOrDefault(t => new Tile(t.Index.Col, t.Index.Row, t.Index.Level).X == 68644 &&
                                     new Tile(t.Index.Col, t.Index.Row, t.Index.Level).Y == 45902);

            if (neighborInfo != null && !tileInfosWithNeighbor.Any(t => t.Index.Equals(neighborInfo.Index)))
            {
                tileInfosWithNeighbor.Add(neighborInfo);
            }

            tileInfos = tileInfosWithNeighbor.ToArray();
        }

        _output.WriteLine($"--- mode={mode} ---");
        foreach (var info in tileInfos)
        {
            var tile = info.ToTile();
            _output.WriteLine($"   tile {tile.X}/{tile.Y} z={tile.Zoom}");
        }

        var zoomLevel = Zoom;
        var context = new EvaluationContext(zoomLevel, 1f, 0f);
        var drawContext = new EvaluationContext(zoomLevel, 1f, 0f);

        Func<double, double, (double, double)> toScreen = (x, y) =>
        {
            var p = viewport.WorldToScreen(x, y);
            return (p.X, p.Y);
        };

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var result = service.Update(tileInfos, zoomLevel, toScreen).Placement;
        var tree = result.Tree;

        var allSymbols = result.PlacedByLayer.SelectMany(p => p.Value.Select(e => e.Symbol))
            .Concat(result.Rejected).ToList();

        foreach (var symbol in allSymbols.OfType<Symbol>())
        {
            var isWaterName = symbol.StyleName.Contains("water_name", StringComparison.OrdinalIgnoreCase);

            if (isWaterName && symbol is LineSymbol l)
            {
                var env = l.Geometry.EnvelopeInternal;
                _output.WriteLine($"LINE id={l.Id} geom={l.Geometry.GeometryType} " +
                    $"worldEnv=[{env.MinX:F0},{env.MinY:F0} .. {env.MaxX:F0},{env.MaxY:F0}]");
            }
        }

        foreach (var layerPair in result.PlacedByLayer)
        {
            foreach (var entry in layerPair.Value)
            {
                if (entry.Symbol is PointSymbol pointSymbol)
                {
                    PointSymbolRenderer.Draw(canvas, context, pointSymbol, ref tree, toScreen, 0, true,
                        drawContext, toScreen, checkForSpace: false);

                    if (pointSymbol.IconSymbol?.ScreenEnvelope is { } iconEnv && iconEnv.MinX > width - 500 && iconEnv.MinY > 300)
                    {
                        _output.WriteLine($"POINT id={pointSymbol.Id} name='{pointSymbol.Name}' placed env={iconEnv}");
                    }
                }
                else if (entry.Symbol is LineSymbol lineSymbol)
                {
                    if (lineSymbol.StyleName.Contains("water_name", StringComparison.OrdinalIgnoreCase))
                    {
                        _output.WriteLine($"   placed screenEnv={lineSymbol.TextSymbol?.ScreenEnvelope}");
                    }

                    LineSymbolRenderer.Draw(canvas, context, lineSymbol, ref tree, toScreen, 0, true,
                        drawContext, toScreen, entry.Run!);
                }
            }
        }

        canvas.Flush();

        var path = Path.Combine(Utilities.TestFiles, "Results", $"schanzengraben-cross-tile-{mode}.png");
        Directory.CreateDirectory(Path.Combine(Utilities.TestFiles, "Results"));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);

        _output.WriteLine($"written: {path}");
    }

    private static BruTile.Extent RendererExtent(Mapsui.Viewport viewport, ITileSchema schema)
    {
        var point1 = viewport.ScreenToWorld(0, 0);
        var point2 = viewport.ScreenToWorld(viewport.Width, 0);
        var point3 = viewport.ScreenToWorld(0, viewport.Height);
        var point4 = viewport.ScreenToWorld(viewport.Width, viewport.Height);

        var minX = Math.Min(Math.Min(point1.X, point2.X), Math.Min(point3.X, point4.X));
        var minY = Math.Min(Math.Min(point1.Y, point2.Y), Math.Min(point3.Y, point4.Y));
        var maxX = Math.Max(Math.Max(point1.X, point2.X), Math.Max(point3.X, point4.X));
        var maxY = Math.Max(Math.Max(point1.Y, point2.Y), Math.Max(point3.Y, point4.Y));

        return new BruTile.Extent(minX, minY, maxX, maxY);
    }
}
