using BruTile;
using Mapsui.Extensions;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Skia;
using VecTiles.Styles.OpenMapTiles.Extensions;
using Xunit.Abstractions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Temporary visual test: renders the symbol layer of the Lindenhof frame
/// for both tile situations and saves the bitmaps to Files/Results.
/// </summary>
public class LindenhofFrameRenderTests
{
    private static readonly string _path = Utilities.DataFiles;

    private readonly ITestOutputHelper _output;

    public LindenhofFrameRenderTests(ITestOutputHelper output) => _output = output;

    private const int Zoom = 18;
    private const double WorldWidth = 40075016.686;
    private const double WorldHalf = WorldWidth / 2;
    private static double TileSpanWorld => WorldWidth / Math.Pow(2, Zoom);

    [Theory]
    [InlineData(true, "lindenhof-with-91802.png")]
    [InlineData(false, "lindenhof-without-91802.png")]
    public void RenderFrame(bool withTileAbove, string fileName)
    {
        var styleSource = $"file://{_path}/osm-liberty.json";
        var tileSource = new OMTRenderedTileSource(styleSource);
        var symbolsLayer = new RenderedSymbolsLayer(tileSource) { SynchronousPlacement = true };

        double resolution = (17.9).ToResolution();
        double topRow = withTileAbove ? 91802.5 : 91803.5;
        double heightTiles = 1.5, widthTiles = 2.0;

        double centerX = -WorldHalf + (137290.5 + 1.0) * TileSpanWorld;
        double centerY = WorldHalf - (topRow + heightTiles / 2.0) * TileSpanWorld;
        var viewport = new Mapsui.Viewport(centerX, centerY, resolution, 0,
            (int)(widthTiles * 512), (int)(heightTiles * 512));

        var zoomLevel = (int)Math.Floor(viewport.Resolution.ToZoomLevel());
        var context = new EvaluationContext(zoomLevel, 1f, 0f);

        using var bitmap = new SKBitmap((int)(widthTiles * 512), (int)(heightTiles * 512));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        symbolsLayer.SynchronousPlacement = true;

        Func<double, double, (double, double)> toScreen = (x, y) =>
        {
            var p = viewport.WorldToScreen(x, y);
            return (p.X, p.Y);
        };

        var result = symbolsLayer.GetPlacementResult(
            tileSource.Schema.GetTileInfos(RendererExtent(viewport, tileSource.Schema), viewport.Resolution).ToArray(),
            zoomLevel, toScreen);

        var tree = result!.Tree;

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
                    if (lineSymbol.TextSymbol?.Text == "Lindenhof")
                    {
                        LogAngles(lineSymbol, toScreen, placed: true);
                    }

                    LineSymbolRenderer.Draw(canvas, context, lineSymbol, ref tree, toScreen, 0, false,
                        placementRun: entry.Run!);
                }
            }
        }

        foreach (var symbol in result.Rejected)
        {
            if (symbol is LineSymbol rejectedLine && rejectedLine.TextSymbol?.Text == "Lindenhof")
            {
                LogAngles(rejectedLine, toScreen, placed: false);
            }
        }

        canvas.Flush();

        var path = Path.Combine(Utilities.TestFiles, "Results", fileName);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);

        _output.WriteLine($"written: {path}");
    }

    private void LogAngles(VecTiles.Common.Primitives.LineSymbol symbol,
        Func<double, double, (double, double)> toScreen, bool placed)
    {
        var part = symbol.Geometry switch
        {
            NetTopologySuite.Geometries.MultiLineString m => (NetTopologySuite.Geometries.LineString)m.Geometries[0],
            NetTopologySuite.Geometries.LineString s => s,
            _ => null,
        };

        if (part == null)
        {
            return;
        }

        var path = new SKPath();
        var (x, y) = toScreen(part.Coordinates[0].X, part.Coordinates[0].Y);
        path.MoveTo((float)x, (float)y);
        foreach (var c in part.Coordinates.Skip(1))
        {
            (x, y) = toScreen(c.X, c.Y);
            path.LineTo((float)x, (float)y);
        }

        using var measure = new SKPathMeasure(path);
        _output.WriteLine($"Lindenhof contour len={measure.Length:F0} placed={placed}");

        double last = double.NaN;
        for (var s = 0f; s < measure.Length; s += 2f)
        {
            measure.GetPositionAndTangent(s, out _, out var t);
            var a = Math.Atan2(t.Y, t.X) * 180 / Math.PI;
            if (!double.IsNaN(last) && Math.Abs(a - last) > 30 && Math.Abs(a - last) < 330)
            {
                _output.WriteLine($"  s={s:F0}: angle {last:F0} -> {a:F0} (change {Math.Abs(a - last):F0})");
            }

            last = a;
        }
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
