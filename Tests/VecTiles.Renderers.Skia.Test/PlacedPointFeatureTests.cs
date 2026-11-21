using Mapsui;
using Mapsui.Extensions;
using NetTopologySuite.Geometries;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui;
using VecTiles.Controls.Mapsui.Extensions;
using VecTiles.Renderers.Skia;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Tests for <see cref="PlacedPointFeatureTester"/>: the hit test over the
/// placement envelopes returns the placed point symbols as features with the
/// anchor in world coordinates and the drawn icon/label rectangles in screen
/// coordinates, works at the anchor and on the offset label below, respects
/// the draw flags, skips line symbols and maps a click position of a rotated
/// or fractional viewport into the canonical placement space.
/// </summary>
public class PlacedPointFeatureTests
{
    // Canonical resolution of zoom level 14; the tests use it directly, so
    // placement scale is 1 and placement space == unrotated screen space
    private static double CanonicalResolution14 => 14.ToResolution();

    // A 1000 x 1000 logical viewport centered at world origin, unrotated
    private static Viewport Viewport(double resolution, double rotation = 0)
    {
        return new Viewport(0, 0, resolution, rotation, 1000, 1000);
    }

    private static PointSymbol PointSymbolWithEnvelopes(double x = 0, double y = 0,
        bool drawIcon = true, bool drawText = true)
    {
        var point = new Point(x, y);
        var icon = new IconPointSymbol(new Tile(0, 0, 0), 1, point, TestSprite());
        icon.Envelope = new Envelope(-16, 16, -16, 16);
        var text = new TextPointSymbol(new Tile(0, 0, 0), 1, point, "Label");
        // The label sits below the icon (like icon-over-text styles)
        text.Envelope = new Envelope(-20, 20, 16, 40);
        var symbol = new PointSymbol(new Tile(0, 0, 0), 1, point, icon, text);
        symbol.SetDrawFlags(drawIcon, drawText);
        return symbol;
    }

    private static ISprite TestSprite()
    {
        return new Sprite(new Bitmap([1]), 0, 0, 19, 19);
    }

    private static PlacementResult PlacementOf((string Layer, PointSymbol Symbol)[] entries,
        int zoomLevel = 14)
    {
        var placedByLayer = new Dictionary<string, List<PlacementEntry>>();
        foreach (var (layer, symbol) in entries)
        {
            if (!placedByLayer.TryGetValue(layer, out var list))
            {
                list = new List<PlacementEntry>();
                placedByLayer.Add(layer, list);
            }

            list.Add(new PlacementEntry(symbol, null));
        }

        return new PlacementResult { PlacedByLayer = placedByLayer, ZoomLevel = zoomLevel };
    }

    /// <summary>
    /// Verifies that a <see langword="null" /> placement produces an empty hit list.
    /// </summary>
    [Fact]
    public void NullPlacementReturnsEmptyList()
    {
        var result = PlacedPointFeatureTester.GetPointFeaturesAt(null, 500, 500, Viewport(CanonicalResolution14));

        Assert.Empty(result);
    }

    /// <summary>
    /// Verifies that a click on the anchor builds a feature with layer, symbol, id, title,
    /// world position and the icon rectangle, without a text rectangle.
    /// </summary>
    [Fact]
    public void HitsIconAtTheAnchorAndBuildsTheFeature()
    {
        var symbol = PointSymbolWithEnvelopes();
        var placement = PlacementOf([("water_symbol", symbol)]);

        var hits = PlacedPointFeatureTester.GetPointFeaturesAt(placement, 500, 500, Viewport(CanonicalResolution14));

        var hit = Assert.Single(hits);
        Assert.Equal("water_symbol", hit.StyleLayer);
        Assert.Same(symbol, hit.Symbol);
        Assert.Equal(1, hit.Id);
        Assert.Equal("Label", hit.Title);
        Assert.Equal(0, hit.Point.X, 6);
        Assert.Equal(0, hit.Point.Y, 6);

        // The icon rectangle frames the icon envelope in screen coordinates;
        // the text part was not hit, so its rectangle is not reported
        Assert.NotNull(hit.IconRectangle);
        Assert.Equal(484, hit.IconRectangle!.MinX, 6);
        Assert.Equal(484, hit.IconRectangle.MinY, 6);
        Assert.Equal(516, hit.IconRectangle.MaxX, 6);
        Assert.Equal(516, hit.IconRectangle.MaxY, 6);
        Assert.Null(hit.TextRectangle);
    }

    /// <summary>
    /// Verifies that a symbol without text and feature name keeps the title empty
    /// and carries the source category from the symbol factory.
    /// </summary>
    [Fact]
    public void KeepsTheTitleEmptyForUnnamedSymbolsAndCarriesTheSourceCategory()
    {
        // Icon without label and without feature name (the icon builder
        // leaves Name empty): the title stays empty, the source category
        // that the symbol factory put on the symbol decides the fallback
        var symbol = PointSymbolWithEnvelopes(drawText: false);
        var pointOnly = new PointSymbol(new Tile(0, 0, 0), 7, symbol.Point,
            symbol.IconSymbol, null)
        {
            // Set by the symbol factory out of the source attributes
            SourceClass = "Kirche"
        };
        pointOnly.SetDrawFlags(true, false);
        var placement = PlacementOf([("Gebaeudepunkt", pointOnly)]);

        var hits = PlacedPointFeatureTester.GetPointFeaturesAt(placement, 500, 500, Viewport(CanonicalResolution14));

        var hit = Assert.Single(hits);
        Assert.Equal(string.Empty, hit.Title);
        Assert.Equal("Kirche", hit.SourceClass);
        Assert.Equal(string.Empty, hit.SourceSubclass);
        Assert.Equal(7, hit.Id);
    }

    /// <summary>
    /// Verifies that a click on the label below the anchor hits the text part
    /// and reports its rectangle, without an icon rectangle.
    /// </summary>
    [Fact]
    public void HitsTextBelowTheAnchor()
    {
        var symbol = PointSymbolWithEnvelopes();
        var placement = PlacementOf([("water_symbol", symbol)]);

        // 20 placement px below the anchor: inside the text envelope (16..40)
        var hits = PlacedPointFeatureTester.GetPointFeaturesAt(placement, 500, 520, Viewport(CanonicalResolution14));

        var hit = Assert.Single(hits);
        Assert.False(hit.IconRectangle != null);
        Assert.NotNull(hit.TextRectangle);
        Assert.Equal(480, hit.TextRectangle!.MinX, 6);
        Assert.Equal(516, hit.TextRectangle.MinY, 6);
        Assert.Equal(520, hit.TextRectangle.MaxX, 6);
        Assert.Equal(540, hit.TextRectangle.MaxY, 6);
    }

    /// <summary>
    /// Verifies that a click outside icon and text envelopes yields no hits.
    /// </summary>
    [Fact]
    public void MissesOutsideEnvelopes()
    {
        var symbol = PointSymbolWithEnvelopes();
        var placement = PlacementOf([("water_symbol", symbol)]);

        // 45 placement px below the anchor: outside both envelopes
        var hits = PlacedPointFeatureTester.GetPointFeaturesAt(placement, 500, 545, Viewport(CanonicalResolution14));

        Assert.Empty(hits);
    }

    /// <summary>
    /// Verifies that a symbol whose icon and text are not drawn is not hit.
    /// </summary>
    [Fact]
    public void MissesWhenBothPartsAreNotDrawn()
    {
        var symbol = PointSymbolWithEnvelopes(drawIcon: false, drawText: false);
        var placement = PlacementOf([("water_symbol", symbol)]);

        var hits = PlacedPointFeatureTester.GetPointFeaturesAt(placement, 500, 500, Viewport(CanonicalResolution14));

        Assert.Empty(hits);
    }

    /// <summary>
    /// Verifies that the hit test only reacts at the screen position of the
    /// anchor, not to world-space proximity alone.
    /// </summary>
    [Fact]
    public void AnchorDistanceIsRespected()
    {
        var inner = PointSymbolWithEnvelopes(0, 0);
        var outer = PointSymbolWithEnvelopes(500 * CanonicalResolution14, 500 * CanonicalResolution14);
        var placement = PlacementOf([("water_symbol", inner), ("poi_label", outer)]);
        var viewport = Viewport(CanonicalResolution14);

        // A click between the two anchors misses both
        var hits = PlacedPointFeatureTester.GetPointFeaturesAt(placement, 700, 700, viewport);
        Assert.Empty(hits);

        // A click on the world position of the outer anchor hits the outer
        // symbol (world units and screen pixels differ by the resolution)
        var screen = viewport.WorldToScreen(outer.Point.X, outer.Point.Y);
        hits = PlacedPointFeatureTester.GetPointFeaturesAt(placement, screen.X, screen.Y, viewport);

        var hit = Assert.Single(hits);
        Assert.Same(outer, hit.Symbol);
    }

    /// <summary>
    /// Verifies that hits at the same anchor are reported with the topmost
    /// style layer first.
    /// </summary>
    [Fact]
    public void ReportsTopMostLayerFirst()
    {
        // Two symbols at the same world anchor: the entry of the later style
        // layer is drawn last (visually on top)
        var bottom = PointSymbolWithEnvelopes(0, 0);
        var top = PointSymbolWithEnvelopes(0, 0);
        var placement = PlacementOf([("bottom", bottom), ("top", top)]);

        var hits = PlacedPointFeatureTester.GetPointFeaturesAt(placement, 500, 500, Viewport(CanonicalResolution14));

        Assert.Equal(2, hits.Count);
        Assert.Equal("top", hits[0].StyleLayer);
        Assert.Equal("bottom", hits[1].StyleLayer);
        Assert.Same(top, hits[0].Symbol);
        Assert.Same(bottom, hits[1].Symbol);
    }

    /// <summary>
    /// Verifies that placed line symbols are not returned by the point hit test.
    /// </summary>
    [Fact]
    public void SkipsLineSymbols()
    {
        var symbol = PointSymbolWithEnvelopes();
        var lineTile = new Tile(0, 0, 0);
        var line = new LineSymbol(lineTile, 2, new LineString(new[] { new Coordinate(0, 0), new Coordinate(10, 10) }), null, null);
        var run = new LineSymbolPlacementRun([Array.Empty<LineSymbolAnchorPlacement>()]);
        var placedByLayer = new Dictionary<string, List<PlacementEntry>>
        {
            ["road_label"] = [new PlacementEntry(line, run)],
            ["water_symbol"] = [new PlacementEntry(symbol, null)],
        };
        var placement = new PlacementResult { PlacedByLayer = placedByLayer, ZoomLevel = 14 };

        var hits = PlacedPointFeatureTester.GetPointFeaturesAt(placement, 500, 500, Viewport(CanonicalResolution14));

        var hit = Assert.Single(hits);
        Assert.Same(symbol, hit.Symbol);
    }

    /// <summary>
    /// Verifies that a fixed pixel offset from the anchor hits at any resolution,
    /// because envelope and click mapping share the canonical placement space.
    /// </summary>
    [Fact]
    public void HitsAtTheSameScreenDistanceAtAnyZoom()
    {
        // The placement space is canonical: a click offset of 8 real pixels
        // from the anchor is inside the 32 px icon at any resolution, because
        // the envelope and the click-to-placement mapping use the same world
        // unit per canonical pixel. Only the anchor moves on the screen.
        var symbol = PointSymbolWithEnvelopes(400 * CanonicalResolution14, 0);
        var placement = PlacementOf([("water_symbol", symbol)]);

        foreach (var resolution in new[] { CanonicalResolution14, CanonicalResolution14 / 4.0 })
        {
            var viewport = Viewport(resolution);
            var anchor = viewport.WorldToScreen(symbol.Point.X, symbol.Point.Y);

            var hits = PlacedPointFeatureTester.GetPointFeaturesAt(placement, anchor.X + 8, anchor.Y, viewport);
            Assert.Single(hits); // 8 px off the anchor: inside the icon

            hits = PlacedPointFeatureTester.GetPointFeaturesAt(placement, anchor.X + 800, anchor.Y, viewport);
            Assert.Empty(hits); // far off the anchor: no hit
        }
    }

    /// <summary>
    /// Verifies that a world position maps through a rotated viewport to the same
    /// hit, with the icon rectangle as the axis-aligned box of the turned rectangle.
    /// </summary>
    [Fact]
    public void HitsTheSameWorldPositionOnARotatedViewport()
    {
        var symbol = PointSymbolWithEnvelopes();
        var placement = PlacementOf([("water_symbol", symbol)]);

        // Click 6 real pixels right of the anchor on an unrotated viewport...
        var unrotated = Viewport(CanonicalResolution14);
        var world = unrotated.ScreenToWorld(506, 500);

        // ...mapped onto a viewport rotated by 90 degrees
        var rotated = Viewport(CanonicalResolution14, 90);
        var screen = rotated.WorldToScreen(world.X, world.Y);

        var hits = PlacedPointFeatureTester.GetPointFeaturesAt(placement, screen.X, screen.Y, rotated);

        var hit = Assert.Single(hits);
        Assert.True(hit.IconRectangle != null);

        // The icon follows the map: on the rotated viewport its rectangle is
        // the axis-aligned box of the turned rectangle (a 32 px icon rotated
        // by 90 degrees around its center keeps its 32 px bounding box)
        var anchor = rotated.WorldToScreen(symbol.Point.X, symbol.Point.Y);
        Assert.Equal(anchor.X - 16, hit.IconRectangle.MinX, 6);
        Assert.Equal(anchor.Y - 16, hit.IconRectangle.MinY, 6);
        Assert.Equal(anchor.X + 16, hit.IconRectangle.MaxX, 6);
        Assert.Equal(anchor.Y + 16, hit.IconRectangle.MaxY, 6);
    }
}
