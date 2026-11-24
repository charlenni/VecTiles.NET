using NetTopologySuite.Geometries;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Skia;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Unit tests for the placement pass (SymbolPlacementEngine) with synthetic
/// symbol layers, without tiles from a real data source.
/// </summary>
public class SymbolPlacementEngineTests
{
    private static readonly Tile Tile = new(0, 0, 14);

    private const int ZoomLevel = 17;

    // Identity converter: world coordinates are used as screen coordinates,
    // so the envelopes of the test symbols can be given in screen coordinates
    private static (double, double) WorldToScreen(double x, double y) => (x, y);

    /// <summary>
    /// A point symbol with only text; the envelope is preset, so no font
    /// measuring is needed. The envelope is relative to the anchor (0, 0).
    /// </summary>
    private static PointSymbol TextSymbol(ulong id, double x, double y, Envelope envelope)
    {
        return new PointSymbol(Tile, id, new Point(x, y), null,
            new TextPointSymbol(Tile, id, new Point(x, y), $"text-{id}") { Envelope = envelope });
    }

    private static RenderedTile Layer(params (string Name, PointSymbol[] Symbols)[] layers)
    {
        var renderedTile = new RenderedTile(Tile);

        foreach (var (name, symbols) in layers)
        {
            renderedTile.Layers.Add(new RenderedSymbolLayer(name, new SymbolLayer(symbols)));
        }

        return renderedTile;
    }

    private static PlacementResult Compute(IRenderedTile tile)
    {
        return SymbolPlacementEngine.Compute([tile], ZoomLevel, WorldToScreen);
    }

    /// <summary>
    /// Verifies that two colliding symbols are resolved in favor of the higher
    /// style layer; the lower symbol is rejected.
    /// </summary>
    [Fact]
    public void Collision_HigherStyleLayerWins()
    {
        // Two symbols at the same location: one in the bottom style layer and
        // one in the top style layer. The top layer is placed first and must
        // win; the bottom symbol is rejected.
        var area = new Envelope(-10, 10, -10, 10);
        var low = TextSymbol(1, 100, 100, area);
        var high = TextSymbol(2, 100, 100, area);

        var result = Compute(Layer(
            ("place_other_low", [low]),
            ("place_city_high", [high])));

        var placedHigh = result.PlacedByLayer["place_city_high"];
        var placedLow = result.PlacedByLayer["place_other_low"];

        var placedPoint = Assert.Single(placedHigh);
        Assert.Same(high, placedPoint.Symbol);
        Assert.Empty(placedLow);
        Assert.Contains(low, result.Rejected);
    }

    /// <summary>
    /// Verifies that <see cref="PlacementResult.PlacedByLayer" /> lists the
    /// style layers in ascending style order when there are no collisions.
    /// </summary>
    [Fact]
    public void PlacedByLayer_KeepsAscendingStyleOrder()
    {
        // Three symbols at distinct locations without collisions: the result
        // must list the layers in ascending style order, so drawing in
        // dictionary order draws bottom layer first, top layer last
        var result = Compute(Layer(
            ("place_low", [TextSymbol(1, 100, 100, new Envelope(-10, 10, -10, 10))]),
            ("place_mid", [TextSymbol(2, 500, 500, new Envelope(-10, 10, -10, 10))]),
            ("place_high", [TextSymbol(3, 900, 900, new Envelope(-10, 10, -10, 10))])));

        Assert.Equal(new[] { "place_low", "place_mid", "place_high" },
            result.PlacedByLayer.Keys.ToArray());
        Assert.All(result.PlacedByLayer.Values, layer => Assert.NotEmpty(layer));
    }

    /// <summary>
    /// A point symbol with only text flagged as the caption of an area feature
    /// (polygon); the envelope is preset, so no font measuring is needed.
    /// </summary>
    private static PointSymbol AreaSymbol(Tile tile, ulong id, double x, double y, string text)
    {
        return new PointSymbol(tile, id, new Point(x, y), null,
            new TextPointSymbol(tile, id, new Point(x, y), text)
            {
                IsAreaCaption = true,
                Envelope = new Envelope(-20, 20, -8, 8),
            });
    }

    /// <summary>
    /// Verifies that the clipped copies of the same area caption (polygon)
    /// in two tiles are deduplicated per text: only the first copy is placed,
    /// the copy of the neighbour tile is rejected. A polygon spanning several
    /// tiles is clipped per tile with no feature id, so its caption anchors
    /// (the interior points of the clipped geometries) differ per tile and
    /// would otherwise draw the same area name once per tile.
    /// </summary>
    [Fact]
    public void AreaCaptionWithoutId_DeduplicatedPerTextAcrossTiles()
    {
        // Tile 0/0 and the diagonal neighbour 1/1 share only a corner: the
        // park copies near the corner carry the same text at different
        // anchors that do not collide
        var tileA = new Tile(0, 0, 18);
        var tileB = new Tile(1, 1, 18);
        var copyA = AreaSymbol(tileA, 0, 260, 260, "Platzpromenade");
        var copyB = AreaSymbol(tileB, 0, 760, 760, "Platzpromenade");

        var renderedA = new RenderedTile(tileA);
        renderedA.Layers.Add(new RenderedSymbolLayer("park_name", new SymbolLayer([copyA])));
        var renderedB = new RenderedTile(tileB);
        renderedB.Layers.Add(new RenderedSymbolLayer("park_name", new SymbolLayer([copyB])));

        var result = SymbolPlacementEngine.Compute([renderedA, renderedB], ZoomLevel, WorldToScreen);

        var placed = result.PlacedByLayer["park_name"];
        var placedPoint = Assert.Single(placed);
        Assert.Same(copyA, placedPoint.Symbol);
        Assert.Contains(copyB, result.Rejected);
    }

    /// <summary>
    /// Verifies that the deduplication per text only groups copies of the
    /// same area caption: two areas with different texts are both placed,
    /// even without feature ids.
    /// </summary>
    [Fact]
    public void AreaCaptionWithoutId_DifferentTextsBothPlaced()
    {
        var tileA = new Tile(0, 0, 18);
        var tileB = new Tile(1, 1, 18);
        var copyA = AreaSymbol(tileA, 0, 260, 260, "Platzpromenade");
        var copyB = AreaSymbol(tileB, 0, 1000, 1000, "Hirschengraben");

        var renderedA = new RenderedTile(tileA);
        renderedA.Layers.Add(new RenderedSymbolLayer("park_name", new SymbolLayer([copyA])));
        var renderedB = new RenderedTile(tileB);
        renderedB.Layers.Add(new RenderedSymbolLayer("park_name", new SymbolLayer([copyB])));

        var result = SymbolPlacementEngine.Compute([renderedA, renderedB], ZoomLevel, WorldToScreen);

        Assert.Equal(2, result.PlacedByLayer["park_name"].Count);
        Assert.Empty(result.Rejected);
    }
}
