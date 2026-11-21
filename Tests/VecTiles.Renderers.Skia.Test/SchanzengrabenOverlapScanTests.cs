using BruTile;
using Mapsui.Extensions;
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
/// Pan/zoom scan over the Schanzengraben area at zoom band 17, reporting any
/// pair of placed symbols whose collision envelopes overlap.
/// </summary>
public class SchanzengrabenOverlapScanTests
{
    private static readonly string _path = Utilities.DataFiles;

    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes the test with the xUnit output helper.
    /// </summary>
    /// <param name="output">The xUnit output helper.</param>
    public SchanzengrabenOverlapScanTests(ITestOutputHelper output) => _output = output;

    private const double WorldWidth = 40075016.686;
    private const double WorldHalf = WorldWidth / 2;

    /// <summary>
    /// Scans the Schanzengraben area with a panning viewport over a grid of small
    /// steps and reports the drawn symbols that overlap other collision boxes.
    /// </summary>
    /// <param name="fractionalZoom">The fractional zoom of the viewport.</param>
    [Theory]
    [InlineData(17.0)]
    [InlineData(17.25)]
    [InlineData(17.5)]
    public void ScanForOverlappingPlacements(double fractionalZoom)
    {
        var styleSource = $"file://{_path}/osm-liberty.json";
        var tileSource = new OMTRenderedTileSource(styleSource);
        var service = new PlacementService(tileSource) { SynchronousPlacement = true };

        double resolution = fractionalZoom.ToResolution();
        const int width = 1024, height = 768;
        double tileSpan = WorldWidth / Math.Pow(2, 17);

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        var overlaps = 0;
        var frames = 0;

        // Pan the tile 17/68642/45902 area in small steps
        for (var dx = -0.5; dx <= 0.5; dx += 0.05)
        {
            for (var dy = -0.5; dy <= 0.5; dy += 0.05)
            {
                // Center between tiles x=68641..68643 shifted by dx tiles
                double centerX = -WorldHalf + (68642 + 0.5 + dx) * tileSpan;
                double centerY = WorldHalf - (45902 + 0.5 + dy) * tileSpan;
                var viewport = new Mapsui.Viewport(centerX, centerY, resolution, 0, width, height);

                var zoomLevel = (int)Math.Floor(resolution.ToZoomLevel() + 1e-6);
                var context = new EvaluationContext(zoomLevel, 1f, 0f);
                var placementScale = (float)(zoomLevel.ToResolution() / resolution);
                var drawContext = new EvaluationContext((float)resolution.ToZoomLevel(), 1f, 0f);

                Func<double, double, (double, double)> toScreen = (x, y) =>
                {
                    var p = viewport.WorldToScreen(x, y);
                    return (p.X / placementScale, p.Y / placementScale);
                };
                Func<double, double, (double, double)> toDraw = (x, y) =>
                {
                    var p = viewport.WorldToScreen(x, y);
                    return (p.X, p.Y);
                };

                var result = service.Update(
                    tileSource.Schema.GetTileInfos(RendererExtent(viewport, tileSource.Schema), viewport.Resolution).ToArray(),
                    zoomLevel, toScreen).Placement;

                var tree = result.Tree;

                foreach (var layerPair in result.PlacedByLayer)
                {
                    foreach (var entry in layerPair.Value)
                    {
                        if (entry.Symbol is PointSymbol pointSymbol)
                        {
                            PointSymbolRenderer.Draw(canvas, context, pointSymbol, ref tree, toScreen, 0, false,
                                drawContext, toDraw, checkForSpace: false);
                        }
                        else if (entry.Symbol is LineSymbol lineSymbol)
                        {
                            LineSymbolRenderer.Draw(canvas, context, lineSymbol, ref tree, toScreen, 0, false,
                                drawContext, toDraw, entry.Run!);
                        }
                    }
                }

                frames++;

                // Collect placed envelopes: label boxes (per-copy) and icons
                var placed = tree.Query(new NetTopologySuite.Geometries.Envelope(-100000, 100000, -100000, 100000))
                    .OfType<Symbol>().Where(s => s.ScreenEnvelope != null).ToList();

                foreach (var a in placed)
                {
                    foreach (var b in placed)
                    {
                        if (ReferenceEquals(a, b))
                        {
                            continue;
                        }

                        // Compare only different features; and only "real" overlaps:
                        // a point icon vs a label box, or label vs label of a
                        // different feature/text
                        if (a.Id == b.Id && a.Name == b.Name)
                        {
                            continue;
                        }

                        if (!a.ScreenEnvelope!.Intersects(b.ScreenEnvelope!))
                        {
                            continue;
                        }

                        var aIsIcon = a is IconPointSymbol;
                        var bIsIcon = b is IconPointSymbol;

                        if (!aIsIcon && !bIsIcon)
                        {
                            continue;
                        }

                        // Ignore empty texts (icon-only copies are fine: icon copy
                        // and text copy carry the same id)
                        if (a.Id == b.Id)
                        {
                            continue;
                        }

                        overlaps++;
                        if (overlaps < 20)
                        {
                            _output.WriteLine($"[{fractionalZoom}] dx={dx:F2} dy={dy:F2} OVERLAP " +
                                $"{a.GetType().Name} id={a.Id} text={GetText(a)} env={a.ScreenEnvelope}  <->  " +
                                $"{b.GetType().Name} id={b.Id} text={GetText(b)} env={b.ScreenEnvelope}");
                        }
                        else
                        {
                            return; // far too many; really broken
                        }
                    }
                }
            }
        }

        _output.WriteLine($"[{fractionalZoom}] frames={frames} overlaps={overlaps}");

        Assert.Equal(0, overlaps);
    }

    private static string GetText(Symbol symbol) => symbol switch
    {
        TextLineSymbol t => t.Text,
        TextPointSymbol tp => tp.Text,
        IconPointSymbol => "",
        IconLineSymbol => "",
        _ => "?",
    };

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
