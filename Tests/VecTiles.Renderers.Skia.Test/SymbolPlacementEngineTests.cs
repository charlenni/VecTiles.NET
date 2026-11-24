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
    private static PointSymbol TextSymbol(ulong id, double x, double y, Envelope envelope,
        double sortOrder = 0, bool allowOverlap = false, bool allowOthers = false)
    {
        return new PointSymbol(Tile, id, new Point(x, y), null,
            new TextPointSymbol(Tile, id, new Point(x, y), $"text-{id}")
            {
                Envelope = envelope,
                AllowOverlap = allowOverlap,
                AllowOthers = allowOthers,
            })
        {
            // The mapsforge placement order reads the SortOrder of the
            // wrapper symbol (see SymbolPlacementEngine.Compute)
            SortOrder = sortOrder,
        };
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

    private static PlacementResult Compute(IRenderedTile tile, bool orderByPriority = false)
    {
        return SymbolPlacementEngine.Compute([tile], ZoomLevel, WorldToScreen,
            orderByPriority: orderByPriority);
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
    /// Verifies the mapsforge priority mode: two colliding symbols in
    /// different style layers are ordered by render priority (ascending
    /// <see cref="ISymbol.SortOrder" />), so the higher priority symbol wins
    /// even when it sits in the lower style layer. The default mode must keep
    /// deciding by style layer order.
    /// </summary>
    [Fact]
    public void MapsforgeMode_HigherPriorityBeatsHigherStyleLayer()
    {
        var area = new Envelope(-10, 10, -10, 10);

        // Same location: "lowPriority" has the worse priority (higher
        // SortOrder) but sits in the top style layer, "highPriority" has the
        // better priority but sits in the bottom style layer
        var lowPriority = TextSymbol(1, 100, 100, area, sortOrder: 10);
        var highPriority = TextSymbol(2, 100, 100, area, sortOrder: 1);

        var tile = Layer(
            ("place_low", [highPriority]),
            ("place_high", [lowPriority]));

        // Default mode: the top style layer wins, priorities are ignored
        var defaultResult = Compute(tile);
        Assert.Same(lowPriority, Assert.Single(defaultResult.PlacedByLayer["place_high"]).Symbol);
        Assert.Empty(defaultResult.PlacedByLayer["place_low"]);
        Assert.Contains(highPriority, defaultResult.Rejected);

        // Mapsforge mode: the better priority wins, the style layer is only
        // the last tie-breaker
        var mapsforgeResult = Compute(tile, orderByPriority: true);
        Assert.Same(highPriority, Assert.Single(mapsforgeResult.PlacedByLayer["place_low"]).Symbol);
        Assert.Empty(mapsforgeResult.PlacedByLayer["place_high"]);
        Assert.Contains(lowPriority, mapsforgeResult.Rejected);
    }

    /// <summary>
    /// Verifies the mapsforge x tie-break: two symbols of the same priority
    /// that clash are ordered by descending x, so the right symbol is placed
    /// first and wins regardless of the style layer order.
    /// </summary>
    [Fact]
    public void MapsforgeMode_SamePriorityRighterSymbolWins()
    {
        // Two symbols 10 px apart with a 20 px wide envelope: they overlap by
        // half. The right symbol sits in the bottom style layer.
        var left = TextSymbol(1, 100, 100, new Envelope(-10, 10, -10, 10));
        var right = TextSymbol(2, 110, 100, new Envelope(-10, 10, -10, 10));

        var tile = Layer(
            ("place_low", [right]),
            ("place_high", [left]));

        // Default mode: the top style layer (left symbol) wins
        var defaultResult = Compute(tile);
        Assert.Same(left, Assert.Single(defaultResult.PlacedByLayer["place_high"]).Symbol);
        Assert.Empty(defaultResult.PlacedByLayer["place_low"]);

        // Mapsforge mode: the right symbol (higher x) wins
        var mapsforgeResult = Compute(tile, orderByPriority: true);
        Assert.Same(right, Assert.Single(mapsforgeResult.PlacedByLayer["place_low"]).Symbol);
        Assert.Empty(mapsforgeResult.PlacedByLayer["place_high"]);
        Assert.Contains(left, mapsforgeResult.Rejected);
    }

    /// <summary>
    /// Verifies the mapsforge display classes in the priority mode: a symbol
    /// of display "always" (overlap in both directions allowed) clashes with
    /// nothing, a symbol of display "order" (overlap of others allowed, but
    /// removed under "always"/"order" symbols) does not clash with "ifspace"
    /// symbols, but two "order" symbols clash with each other.
    /// </summary>
    [Fact]
    public void MapsforgeMode_DisplayAlwaysAndOrderSemantics()
    {
        var area = new Envelope(-10, 10, -10, 10);

        // "always" (class 2), "order" (class 1) and "ifspace" (class 0)
        var always = TextSymbol(1, 100, 100, area, allowOverlap: true, allowOthers: true);
        var order = TextSymbol(2, 100, 100, area, allowOverlap: true);
        var ifSpace = TextSymbol(3, 100, 100, area);

        // "always" and "order" overlap the "ifspace" symbol: nothing clashes,
        // all three are placed
        var first = Compute(Layer(
            ("place_a", [always]),
            ("place_b", [order]),
            ("place_c", [ifSpace])), orderByPriority: true);
        Assert.NotEmpty(first.PlacedByLayer["place_a"]);
        Assert.NotEmpty(first.PlacedByLayer["place_b"]);
        Assert.NotEmpty(first.PlacedByLayer["place_c"]);
        Assert.Empty(first.Rejected);

        // Two "order" symbols at the same spot clash: the symbol of the top
        // style layer is processed first (x tie-break falls back to the
        // layer order), the other one is rejected
        var order2 = TextSymbol(4, 100, 100, area, allowOverlap: true);
        var second = Compute(Layer(
            ("place_a", [order]),
            ("place_b", [order2])), orderByPriority: true);
        Assert.NotEmpty(second.PlacedByLayer["place_b"]);
        Assert.Empty(second.PlacedByLayer["place_a"]);
        Assert.Contains(order, second.Rejected);
    }
}
