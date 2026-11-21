using BruTile;
using Mapsui.Extensions;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Skia;
using Xunit.Abstractions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Visual test: renders the Schanzengraben area (tile 17/68642/45902) at zoom 17
/// and logs the placement of the water_name_line label and the poi_z16 point
/// symbol to track overlapping placements.
/// </summary>
public class SchanzengrabenFrameRenderTests
{
    private static readonly string _path = Utilities.DataFiles;

    private readonly ITestOutputHelper _output;

    public SchanzengrabenFrameRenderTests(ITestOutputHelper output) => _output = output;

    private const int Zoom = 17;
    private const double WorldWidth = 40075016.686;
    private const double WorldHalf = WorldWidth / 2;
    private static double TileSpanWorld => WorldWidth / Math.Pow(2, Zoom);

    [Theory]
    [InlineData(17.0, 1024, 768)]
    [InlineData(17.1, 1024, 768)]
    [InlineData(17.2, 1024, 768)]
    [InlineData(17.3, 1024, 768)]
    [InlineData(17.4, 1024, 768)]
    [InlineData(17.5, 1024, 768)]
    [InlineData(17.6, 1024, 768)]
    [InlineData(17.7, 1024, 768)]
    [InlineData(17.8, 1024, 768)]
    [InlineData(17.9, 1024, 768)]
    [InlineData(17.0, 1536, 1536)]
    [InlineData(17.0, 512, 512)]
    public void RenderSchanzengrabenFrame(double fractionalZoom, double width, double height)
    {
        var styleSource = $"file://{_path}/osm-liberty.json";
        var tileSource = new OMTRenderedTileSource(styleSource);
        var service = new PlacementService(tileSource) { SynchronousPlacement = true };

        double resolution = fractionalZoom.ToResolution();
        double heightTiles = height / 512, widthTiles = width / 512;

        // Center the viewport on the middle of tile 17/68642/45902 (XYZ)
        double centerX = -WorldHalf + (68642 + widthTiles / 2) * TileSpanWorld;
        double centerY = WorldHalf - (45902 + heightTiles / 2) * TileSpanWorld;
        var viewport = new Mapsui.Viewport(centerX, centerY, resolution, 0,
            (int)(widthTiles * 512), (int)(heightTiles * 512));

        var zoomLevel = (int)Math.Floor(resolution.ToZoomLevel() + 1e-6);
        var context = new EvaluationContext(zoomLevel, 1f, 0f);

        var placementScale = (float)(zoomLevel.ToResolution() / resolution);
        var drawContext = new EvaluationContext((float)resolution.ToZoomLevel(), 1f, 0f);

        using var bitmap = new SKBitmap((int)width, (int)height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

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

        var tree = result!.Tree;
        var order = 0;

        foreach (var layerPair in result.PlacedByLayer)
        {
            foreach (var entry in layerPair.Value)
            {
                var symbol = (Symbol)entry.Symbol;
                var name = symbol.Name;
                var styleName = symbol.StyleName;
                var text = symbol switch
                {
                    LineSymbol l => l.TextSymbol?.Text ?? "?",
                    PointSymbol p => p.TextSymbol?.Text ?? "?",
                    _ => "?",
                };
                var isWaterName = LinqContains(styleName, "water_name");
                var isPoi = symbol.Id == 4127302961UL || LinqContains(styleName, "poi_z16");

                if (isWaterName || isPoi)
                {
                    _output.WriteLine($"""## {order} id={symbol.Id} style='{symbol.StyleName}' geo='{name}' text='{text}' sort={symbol.SortOrder}""");
                }

                if (symbol is PointSymbol pointSymbol)
                {
                    if (isPoi)
                    {
                        _output.WriteLine($"   POINT placed iconEnv={pointSymbol.IconSymbol?.ScreenEnvelope} " +
                            $"textEnv={pointSymbol.TextSymbol?.ScreenEnvelope} allowOthers={pointSymbol.AllowOthers}");
                    }

                    PointSymbolRenderer.Draw(canvas, context, pointSymbol, ref tree, toScreen, 0, false,
                        drawContext, toDraw, checkForSpace: false);
                }
                else if (symbol is LineSymbol lineSymbol)
                {
                    if (isWaterName)
                    {
                        _output.WriteLine($"   LINE placed geom={lineSymbol.Geometry.GeometryType} " +
                            $"env={lineSymbol.TextSymbol?.ScreenEnvelope}");
                    }

                    LineSymbolRenderer.Draw(canvas, context, lineSymbol, ref tree, toScreen, 0, true,
                        drawContext, toDraw, entry.Run!);
                }

                order++;
            }
        }

        foreach (var symbol in result.Rejected.OfType<Symbol>())
        {
            var styleName = symbol.StyleName;

            if (LinqContains(styleName, "water_name") || symbol.Id == 4127302961UL || LinqContains(styleName, "poi_z16"))
            {
                var text = symbol switch
                {
                    LineSymbol l => l.TextSymbol?.Text ?? "?",
                    PointSymbol p => p.TextSymbol?.Text ?? "?",
                    _ => "?",
                };
                _output.WriteLine($"   REJECTED style='{symbol.StyleName}' text='{text}' " +
                    $"iconEnv={(symbol is PointSymbol ps ? ps.IconSymbol?.ScreenEnvelope : null)}");
            }
        }

        canvas.Flush();

        // Diagnostics: list all collision-tree entries intersecting the placed
        // Schanzengraben label boxes and the placed icons of the poi_z16
        // features of interest
        var schanzenvs = tree.Query(new NetTopologySuite.Geometries.Envelope(0, (widthTiles * 512), 0, (heightTiles * 512)))
            .OfType<Symbol>()
            .Where(s => s.ScreenEnvelope != null)
            .ToList();

        foreach (var label in schanzenvs.Where(s => s is TextLineSymbol t && t.Text == "Schanzengraben"))
        {
            var env = label.ScreenEnvelope!;
            _output.WriteLine($"LABEL BOX {env}");

            foreach (var other in schanzenvs.Where(s => !ReferenceEquals(s, label) && s.ScreenEnvelope!.Intersects(env)))
            {
                _output.WriteLine($"   OVERLAP {other.GetType().Name} id={other.Id} name='{other.Name}' text='{GetText(other)}' " +
                    $"style='{other.StyleName}' screenEnv={other.ScreenEnvelope}");
            }
        }

        // Any placed icon overlapping a placed Schanzengraben label box?
        foreach (var label in schanzenvs.Where(s => s is TextLineSymbol t && t.Text == "Schanzengraben"))
        {
            foreach (var icon in schanzenvs.Where(s => s is IconPointSymbol && s.ScreenEnvelope!.Intersects(label.ScreenEnvelope!)))
            {
                _output.WriteLine($"!!! ICON-LABEL OVERLAP at zoom {fractionalZoom}: icon id={icon.Id} " +
                    $"env={icon.ScreenEnvelope} label env={label.ScreenEnvelope}");
            }
        }

        var path = Path.Combine(Utilities.TestFiles, "Results", $"schanzengraben-frame-{fractionalZoom:F1}-{width:F0}x{height:F0}.png");
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);

        _output.WriteLine($"written: {path}");
    }

    private static string GetText(Symbol symbol) => symbol switch
    {
        TextLineSymbol t => t.Text,
        TextPointSymbol tp => tp.Text,
        IconPointSymbol => "",
        IconLineSymbol => "",
        _ => "?",
    };

    private static bool LinqContains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

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
