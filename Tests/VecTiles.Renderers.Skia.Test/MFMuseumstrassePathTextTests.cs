using BruTile;
using Mapsui.Extensions;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Skia;
using Xunit.Abstractions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Regression test: with the Mapsforge osmarender theme no street-name label of
/// the Museumstrasse (tile 17/68645/45898) was placed at the SampleApp's start
/// zoom 17.9, although the other streets of the same style layer kept theirs.
/// The way labels lost them against the oneway-arrow icons of the same street
/// that were placed first (equal priority sorted by position before the style
/// layer) and against point labels over them, leaving every piece of the street
/// without its name. Mapsforge places the way labels before the point labels
/// (LabelPlacement.placeLabels(waySymbols, pointSymbols)) and draws the oneway
/// arrows with the ways, without any label claim, so the way label always wins
/// its own street.
/// </summary>
public class MFMuseumstrassePathTextTests
{
    private readonly ITestOutputHelper _output;

    public MFMuseumstrassePathTextTests(ITestOutputHelper output) => _output = output;

    private static readonly string MapFile = Path.Combine(Utilities.DataFiles, "..",
        "Mapsforge", "zuerich.map");
    private static readonly string Theme = Path.Combine(Utilities.DataFiles, "..",
        "Mapsforge", "mapsforge", "osmarender.xml");

    private const double WorldWidth = 40075016.686;
    private const double WorldHalf = WorldWidth / 2;

    /// <summary>
    /// Centers the 3x3 tile view on the middle of (z18) tile 137290/91797 —
    /// the Museumstrasse north of Zürich HB — at the given (fractional) zoom.
    /// </summary>
    private static (Mapsui.Viewport Viewport, int ZoomLevel, float PlacementScale) ViewportOnMuseumstrasse(
        double zoom)
    {
        var resolution = zoom.ToResolution();
        double anchor = 1.0 / Math.Pow(2, 18); // half tile span of the anchor tile at z18
        double centerX = -WorldHalf + (137290 + 0.5) * WorldWidth * anchor;
        double centerY = WorldHalf - (91797 + 0.5) * WorldWidth * anchor;
        var viewport = new Mapsui.Viewport(centerX, centerY, resolution, 0, 3 * 512, 3 * 512);

        var zoomLevel = (int)Math.Floor(viewport.Resolution.ToZoomLevel() + 1e-6);
        var placementScale = (float)(zoomLevel.ToResolution() / resolution);

        return (viewport, zoomLevel, placementScale);
    }

    [Theory]
    [InlineData(17.0)]
    [InlineData(17.9)]
    [InlineData(18.0)]
    public void MuseumstrasseWayLabel_IsPlaced(double zoom)
    {
        using var tileSource = new MFRenderedTileSource(MapFile, new IdentityTagsConverter("mapsforge"), Theme);
        var service = new PlacementService(tileSource) { SynchronousPlacement = true };
        var (viewport, zoomLevel, placementScale) = ViewportOnMuseumstrasse(zoom);

        Func<double, double, (double, double)> toScreen = (x, y) =>
        {
            var p = viewport.WorldToScreen(x, y);
            return (p.X / placementScale, p.Y / placementScale);
        };

        var result = service.Update(
            tileSource.Schema.GetTileInfos(ExtentOn(viewport, tileSource.Schema), viewport.Resolution).ToArray(),
            zoomLevel, toScreen).Placement;

        var placedTexts = result.PlacedByLayer
            .SelectMany(pair => pair.Value)
            .Select(entry => entry.Symbol)
            .OfType<LineSymbol>()
            .Where(symbol => symbol.TextSymbol?.Text?.Contains("Museumstrasse") == true)
            .ToList();

        var rejectedTexts = result.Rejected
            .OfType<LineSymbol>()
            .Where(symbol => symbol.TextSymbol?.Text?.Contains("Museumstrasse") == true)
            .ToList();

        _output.WriteLine($"zoom {zoom}: placed={placedTexts.Count} rejected={rejectedTexts.Count} " +
            $"pathText165Placed={result.PlacedByLayer.GetValueOrDefault("pathText165")?.Count ?? 0}");

        Assert.NotEmpty(placedTexts);
    }

    private static BruTile.Extent ExtentOn(Mapsui.Viewport viewport, ITileSchema schema)
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
