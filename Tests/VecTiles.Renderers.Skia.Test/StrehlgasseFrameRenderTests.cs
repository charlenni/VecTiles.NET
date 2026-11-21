using BruTile;
using Mapsui.Extensions;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Skia.Primitives;
using Xunit.Abstractions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Visual test: renders the Strehlgasse / Beldona area (tile 18/137291/91804)
/// and logs the placement of both symbols to track overlapping placements.
/// </summary>
public class StrehlgasseFrameRenderTests
{
    private static readonly string _path = Utilities.DataFiles;
    
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes the test with the xUnit output helper.
    /// </summary>
    /// <param name="output">The xUnit output helper.</param>
    public StrehlgasseFrameRenderTests(ITestOutputHelper output) => _output = output;

    private const int Zoom = 18;
    private const double WorldWidth = 40075016.686;
    private const double WorldHalf = WorldWidth / 2;
    private static double TileSpanWorld => WorldWidth / Math.Pow(2, Zoom);

    /// <summary>
    /// Renders the Strehlgasse frame at zoom 17.9 and logs the placement of both
    /// symbols to the test output for overlap analysis.
    /// </summary>
    [Fact]
    public void RenderStrehlgasseFrame()
    {
        var styleSource = $"file://{_path}/osm-liberty.json";
        var tileSource = new OMTRenderedTileSource(styleSource);
        var service = new PlacementService(tileSource) { SynchronousPlacement = true };

        double resolution = (17.9).ToResolution();
        double heightTiles = 2.0, widthTiles = 2.0;

        // Center the viewport on the middle of tile 18/137291/91804
        double centerX = -WorldHalf + (137291 + 1.0) * TileSpanWorld;
        double centerY = WorldHalf - (91804 + 1.0) * TileSpanWorld;
        var viewport = new Mapsui.Viewport(centerX, centerY, resolution, 0,
            (int)(widthTiles * 512), (int)(heightTiles * 512));

        var zoomLevel = (int)Math.Floor(viewport.Resolution.ToZoomLevel() + 1e-6);
        var context = new EvaluationContext(zoomLevel, 1f, 0f);

        // Placement in the canonical screen space of the integer zoom, style
        // sizes evaluated at the fractional zoom, like RenderedSymbolsLayerRenderer
        // does (production parity)
        var placementScale = (float)(zoomLevel.ToResolution() / resolution);
        var drawContext = new EvaluationContext((float)viewport.Resolution.ToZoomLevel(), 1f, 0f);

        using var bitmap = new SKBitmap((int)(widthTiles * 512), (int)(heightTiles * 512));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);



        // Placement converter (canonical placement space of the integer zoom)
        Func<double, double, (double, double)> toScreen = (x, y) =>
        {
            var p = viewport.WorldToScreen(x, y);
            return (p.X / placementScale, p.Y / placementScale);
        };

        // Draw converter (real screen space)
        Func<double, double, (double, double)> toDraw = (x, y) =>
        {
            var p = viewport.WorldToScreen(x, y);
            return (p.X, p.Y);
        };

        var result = service.Update(
            tileSource.Schema.GetTileInfos(RendererExtent(viewport, tileSource.Schema), viewport.Resolution).ToArray(),
            zoomLevel, toScreen).Placement;

        var tree = result.Tree;
        var order = 0;

        foreach (var layerPair in result.PlacedByLayer)
        {
            foreach (var entry in layerPair.Value)
            {
                if (entry.Symbol is PointSymbol pointSymbol)
                {
                    var name = pointSymbol.TextSymbol?.Text ?? pointSymbol.Name ?? "?";

                    if (name.Contains("Beldona"))
                    {
                        _output.WriteLine($"#{order} POINT '{name}' placed " +
                            $"iconEnv={pointSymbol.IconSymbol?.ScreenEnvelope} textEnv={pointSymbol.TextSymbol?.ScreenEnvelope}");
                    }

                    PointSymbolRenderer.Draw(canvas, context, pointSymbol, ref tree, toScreen, 0, false,
                        drawContext, toDraw, checkForSpace: false);
                }
                else if (entry.Symbol is LineSymbol lineSymbol)
                {
                    var text = lineSymbol.TextSymbol?.Text ?? "?";

                    if (text.Contains("Strehl"))
                    {
                        _output.WriteLine($"#{order} LINE '{text}' placed");

                        var lineText = (RenderedText)lineSymbol.TextSymbol!.Native!;
                        _output.WriteLine($"    measured W={lineText.MeasuredWidth:F1} H={lineText.MeasuredHeight:F1} " +
                            $"env={lineSymbol.TextSymbol.Envelope} fontSize={lineSymbol.TextSymbol.FontSize.Invoke(context):F1} " +
                            $"anchor=({lineSymbol.TextSymbol.Anchor.X},{lineSymbol.TextSymbol.Anchor.Y}) " +
                            $"padding={lineSymbol.TextSymbol.Padding}");
                    }

                    LineSymbolRenderer.Draw(canvas, context, lineSymbol, ref tree, toScreen, 0, true,
                        drawContext, toDraw, entry.Run!);
                }

                order++;
            }
        }

        foreach (var symbol in result.Rejected)
        {
            var name = symbol switch
            {
                PointSymbol p => p.TextSymbol?.Text ?? p.Name ?? "?",
                LineSymbol l => l.TextSymbol?.Text ?? "?",
                _ => "?",
            };

            if (name.Contains("Beldona") || name.Contains("Strehl"))
            {
                _output.WriteLine($"#{order} REJECTED '{name}'");
            }
        }

        canvas.Flush();

        // Reference dump of the quadtree content around the Beldona text
        var beldonaEnv = new NetTopologySuite.Geometries.Envelope(285.5, 331.7, 272.7, 290.9);
        foreach (var s in tree.Query(beldonaEnv).OfType<Symbol>()
            .Where(s => s.ScreenEnvelope == null || s.ScreenEnvelope.Intersects(beldonaEnv)))
        {
            _output.WriteLine($"TREE {s.GetType().Name} name='{s.Name}' allowOthers={s.AllowOthers} " +
                $"env={(s.Envelope == null ? "null" : s.Envelope.ToString())} screenEnv={(s.ScreenEnvelope == null ? "null" : s.ScreenEnvelope.ToString())}");
        }

        // All boxes of all placed Strehlgasse labels
        foreach (var s in tree.Query(new NetTopologySuite.Geometries.Envelope(0, 1024, 0, 1024)).OfType<Symbol>()
            .Where(s => s.Name == "Strehlgasse"))
        {
            _output.WriteLine($"BOX screenEnv={s.ScreenEnvelope}");
        }

        var path = Path.Combine(Utilities.TestFiles, "Results", "strehlgasse-frame.png");
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);

        _output.WriteLine($"written: {path} tile-center-screen=({toScreen(centerX, centerY).Item1:F0},{toScreen(centerX, centerY).Item2:F0})");
    }

    /// <summary>
    /// Street label placement must be a function of the integer zoom only: at
    /// two different fractional zoom levels within the same zoom band the drawn
    /// labels must keep their anchors (in world coordinates), like MapLibre
    /// keeps placements stable while zooming.
    /// </summary>
    [Fact]
    public void StrehlgasseLabelAnchor_StaysAtSameWorldPositionWhileFractionalZooming()
    {
        const double toleranceWorld = 1e-2;

        // Fixed tile set and zoom band: only the fractional resolution varies,
        // so the test isolates the placement math from tile-set changes
        var (x, y, tileInfos) = PlaceStrehlgasseAnchor(18.1);
        var (otherX, otherY, _) = PlaceStrehlgasseAnchor(18.6, tileInfos);

        Assert.NotNull(x);
        Assert.NotNull(y);

        var deltaX = Math.Abs(x!.Value - otherX!.Value);
        var deltaY = Math.Abs(y!.Value - otherY!.Value);

        Assert.True(deltaX < toleranceWorld && deltaY < toleranceWorld,
            $"Label anchor moved while zooming on fractional zoom levels: " +
            $"dX={deltaX:F6}m dY={deltaY:F6}m exceeds {toleranceWorld}m");
    }

    private (double? x, double? y, TileInfo[] TileInfos) PlaceStrehlgasseAnchor(double fractionalZoom,
        TileInfo[]? tileInfos = null)
    {
        var styleSource = $"file://{_path}/osm-liberty.json";
        var tileSource = new OMTRenderedTileSource(styleSource);
        var service = new PlacementService(tileSource) { SynchronousPlacement = true };

        double resolution = fractionalZoom.ToResolution();
        double heightTiles = 2.0, widthTiles = 2.0;

        double centerX = -WorldHalf + (137291 + 1.0) * TileSpanWorld;
        double centerY = WorldHalf - (91804 + 1.0) * TileSpanWorld;
        var viewport = new Mapsui.Viewport(centerX, centerY, resolution, 0,
            (int)(widthTiles * 512), (int)(heightTiles * 512));

        var zoomLevel = (int)Math.Floor(viewport.Resolution.ToZoomLevel());
        var placementScale = (float)(zoomLevel.ToResolution() / resolution);

        tileInfos ??= tileSource.Schema.GetTileInfos(RendererExtent(viewport, tileSource.Schema), viewport.Resolution).ToArray();

        var context = new EvaluationContext(zoomLevel, 1f, 0f);



        var toScreen = new Func<double, double, (double, double)>((x, y) =>
        {
            var p = viewport.WorldToScreen(x, y);
            return (p.X / placementScale, p.Y / placementScale);
        });

        using var bitmap = new SKBitmap((int)viewport.Width, (int)viewport.Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Scale(placementScale, placementScale);

        var result = service.Update(tileInfos, zoomLevel, toScreen).Placement;
        var tree = result.Tree;

        double? anchorX = null, anchorY = null;

        foreach (var layerPair in result.PlacedByLayer)
        {
            foreach (var entry in layerPair.Value)
            {
                if (entry.Symbol is PointSymbol pointSymbol)
                {
                    PointSymbolRenderer.Draw(canvas, context, pointSymbol, ref tree, toScreen, 0, false);
                }
                else if (entry.Symbol is LineSymbol lineSymbol)
                {
                    if (lineSymbol.TextSymbol?.Text == "Strehlgasse")
                    {
                        _output.WriteLine($"[{fractionalZoom}] LINE placed geomType={lineSymbol.Geometry.GeometryType} " +
                            $"env={lineSymbol.TextSymbol?.ScreenEnvelope}");
                    }

                    LineSymbolRenderer.Draw(canvas, context, lineSymbol, ref tree, toScreen, 0, false,
                        placementRun: entry.Run!);
                }
            }
        }

        // The union envelope of the drawn label was placed in the canonical
        // placement space; converting its center back to world coordinates must
        // give the same anchor regardless of the fractional zoom
        var labelBoxes = tree.Query(new NetTopologySuite.Geometries.Envelope(-16384, 16384, -16384, 16384)).OfType<Symbol>()
            .Where(s => s.Name == "Strehlgasse" && s.ScreenEnvelope != null)
            .Select(s => s.ScreenEnvelope!)
            .ToList();

        _output.WriteLine($"[{fractionalZoom}] placed boxes={labelBoxes.Count}");

        foreach (var box in labelBoxes.OrderBy(b => b.MinX).ThenBy(b => b.MinY))
        {
            var p = viewport.ScreenToWorld(
                (box.MinX + box.MaxX) / 2 * placementScale,
                (box.MinY + box.MaxY) / 2 * placementScale);
            _output.WriteLine($"[{fractionalZoom}] BOX env={box} world=({p.X:F3},{p.Y:F3})");
        }

        if (labelBoxes.Count > 0)
        {
            // Scaling the virtual centers by the zoom ratio gives the real drawn
            // screen positions; ScreenToWorld recovers the label's world anchor
            var worldCenters = labelBoxes.Select(env => viewport.ScreenToWorld(
                (env.MinX + env.MaxX) / 2 * placementScale,
                (env.MinY + env.MaxY) / 2 * placementScale)).ToList();

            anchorX = worldCenters.Average(p => p.X);
            anchorY = worldCenters.Average(p => p.Y);
        }

        return (anchorX, anchorY, tileInfos);
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
