using NetTopologySuite.Geometries;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Skia;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Unit tests for the persistent placement decisions (IncrementalPlacementState):
/// a pass with a state must place exactly like a pass without one, and the
/// placed decisions must survive the following passes, also when the tile set
/// of the viewport changes. Rejections are intra-pass decisions: they are not
/// persisted, so a rejected symbol is re-examined in every pass.
/// </summary>
public class IncrementalPlacementStateTests
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

    private static PlacementResult Compute(IncrementalPlacementState? state,
        params IRenderedTile[] tiles)
    {
        return SymbolPlacementEngine.Compute(tiles, ZoomLevel, WorldToScreen, default, state);
    }

    /// <summary>
    /// Verifies that a first placement pass with a fresh state places exactly
    /// like a pass without a state.
    /// </summary>
    [Fact]
    public void FirstPass_WithFreshState_PlacesLikeWithoutState()
    {
        var state = new IncrementalPlacementState();
        var tiles = new[]
        {
            Layer(("place_low", [TextSymbol(1, 100, 100, new Envelope(-10, 10, -10, 10))])),
            Layer(("place_low", [TextSymbol(2, 500, 500, new Envelope(-10, 10, -10, 10))])),
        };

        var withState = Compute(state, tiles);
        var withoutState = Compute(null, tiles);

        Assert.Equal(withoutState.PlacedByLayer.Keys, withState.PlacedByLayer.Keys);
        Assert.Equal(withoutState.Rejected.Count, withState.Rejected.Count);
        Assert.Equal(
            withoutState.PlacedByLayer.Select(kv => (kv.Key, kv.Value.Select(e => e.Symbol).ToArray())),
            withState.PlacedByLayer.Select(kv => (kv.Key, kv.Value.Select(e => e.Symbol).ToArray())));

        // One decision per examined symbol: twice placed, nothing rejected
        Assert.Equal(2, withState.PlacedByLayer.Values.Sum(v => v.Count));
        Assert.Empty(withState.Rejected);
    }

    /// <summary>
    /// Verifies that a symbol repeated over a tile boundary keeps the placement
    /// decision of the first pass instead of being placed again or moved.
    /// </summary>
    [Fact]
    public void SecondPass_KeepsPlacedPosition_AndDoesNotDecideTwice()
    {
        var state = new IncrementalPlacementState();
        var symbol = TextSymbol(1, 100, 100, new Envelope(-10, 10, -10, 10));
        var tileA = Layer(("place_low", [symbol]));

        var first = Compute(state, tileA);
        var firstEntry = Assert.Single(first.PlacedByLayer["place_low"]);

        // The second pass sees a different tile with the same feature (the
        // source repeats the feature with the same symbol id over the tile
        // boundary): the decision of the first pass stands, the symbol is
        // not placed twice and not moved
        var second = Compute(state, tileA, tileA);

        var placed = second.PlacedByLayer["place_low"];
        var entry = Assert.Single(placed);
        Assert.Same(firstEntry, entry);
        Assert.Empty(second.Rejected);
    }

    /// <summary>
    /// Verifies that a rejection is not kept in the state: a later pass in
    /// which the blocking symbol no longer exists re-examines the symbol and
    /// places it.
    /// </summary>
    [Fact]
    public void SecondPass_WhenBlockerVanishes_RejectedSymbolIsPlaced()
    {
        var state = new IncrementalPlacementState();
        var low = TextSymbol(1, 100, 100, new Envelope(-10, 10, -10, 10));
        // The top style layer is placed first and blocks the area
        var high = TextSymbol(2, 100, 100, new Envelope(-10, 10, -10, 10));

        var first = Compute(state,
            Layer(("place_low", [low]), ("place_high", [high])));

        Assert.Empty(first.PlacedByLayer["place_low"]);
        Assert.Contains(low, first.Rejected);

        // A later pass without the blocking symbol (its tile vanished): the
        // symbol is re-examined and now placed — a rejection is only an
        // intra-pass decision and must not be persisted
        var second = Compute(state, Layer(("place_low", [low])));

        var entry = Assert.Single(second.PlacedByLayer["place_low"]);
        Assert.Same(low, entry.Symbol);
        Assert.Empty(second.Rejected);
    }

    /// <summary>
    /// Verifies that a rejection stays stable while the blocking symbol
    /// remains: the collision tree is grow-only, so the symbol stays rejected
    /// in every following pass and does not flicker.
    /// </summary>
    [Fact]
    public void SecondPass_WithPersistentBlocker_StaysRejected()
    {
        var state = new IncrementalPlacementState();
        var low = TextSymbol(1, 100, 100, new Envelope(-10, 10, -10, 10));
        var high = TextSymbol(2, 100, 100, new Envelope(-10, 10, -10, 10));

        var first = Compute(state,
            Layer(("place_low", [low]), ("place_high", [high])));

        Assert.Empty(first.PlacedByLayer["place_low"]);
        Assert.Contains(low, first.Rejected);

        // The blocking symbol is still there: the collision tree keeps its
        // box, so the low symbol stays rejected in the second pass
        var second = Compute(state,
            Layer(("place_low", [low]), ("place_high", [high])));

        Assert.Empty(second.PlacedByLayer["place_low"]);
        Assert.Contains(low, second.Rejected);
    }

    /// <summary>
    /// Verifies that symbols of a tile that left the viewport keep their decisions:
    /// the remaining features are not re-placed.
    /// </summary>
    [Fact]
    public void SecondPass_WithoutTheTile_KeepsItsDecision()
    {
        var state = new IncrementalPlacementState();
        var visible = TextSymbol(1, 100, 100, new Envelope(-10, 10, -10, 10));
        var hidden = TextSymbol(2, 1000, 1000, new Envelope(-10, 10, -10, 10));
        var tileVisible = Layer(("place_low", [visible]));
        var tileHidden = Layer(("place_low", [hidden]));

        var first = Compute(state, tileVisible, tileHidden);
        var firstVisible = first.PlacedByLayer["place_low"]
            .Single(e => ReferenceEquals(e.Symbol, visible));

        // The tile with the second symbol left the viewport: the entry of
        // the remaining feature stays the same instance (no re-placement)
        var second = Compute(state, tileVisible);

        var entry = Assert.Single(second.PlacedByLayer["place_low"]);
        Assert.Same(firstVisible, entry);
    }

    /// <summary>
    /// Verifies that a re-rendered symbol without feature id (id 0) is recognized
    /// by its anchor (tile, position and text) and keeps its placement instead of
    /// colliding with the box of its own first pass.
    /// </summary>
    [Fact]
    public void SecondPass_AfterReRender_WithoutFeatureId_KeepsThePlacement()
    {
        // The bug of the "Goto Center" report: a view is entered a second
        // time, but its placement result was evicted and its tiles have been
        // re-rendered in the meantime. The re-render produces FRESH symbol
        // instances. Features without an id attribute carry id 0, so the old
        // decision can no longer be found by identity: the fresh instance
        // gets re-examined against the collision tree and collides with the
        // (placed) box of its own first pass! The state must recognize the
        // re-rendered point symbol by its anchor (tile + position + text)
        var state = new IncrementalPlacementState();
        var envelope = new Envelope(-10, 10, -10, 10);
        var firstTile = Layer(("place_city", [TextSymbol(0, 100, 100, envelope)]));

        var first = Compute(state, firstTile);
        Assert.Single(first.PlacedByLayer["place_city"]);

        // Re-render of the same tile: same feature, same anchor, id 0 —
        // new symbol instances
        var secondTile = Layer(("place_city", [TextSymbol(0, 100, 100, envelope)]));

        var second = Compute(state, secondTile);

        var entry = Assert.Single(second.PlacedByLayer["place_city"]);
        Assert.Empty(second.Rejected);
        Assert.Same(first.PlacedByLayer["place_city"][0], entry);
    }

    /// <summary>
    /// Verifies that two id-less symbols at different anchors are recognized as
    /// distinct decisions after a re-render, even when the merge order is swapped.
    /// </summary>
    [Fact]
    public void SecondPass_AfterReRender_WithoutFeatureId_DifferentAnchor_IsPlacedIndependently()
    {
        // Two different labels at different anchors are two decisions — the
        // anchor key must not identify them as the same symbol
        var state = new IncrementalPlacementState();
        var envelope = new Envelope(-10, 10, -10, 10);
        var first = Compute(state, Layer(("place_city",
            [TextSymbol(0, 100, 100, envelope), TextSymbol(0, 500, 500, envelope)])));
        Assert.Equal(2, first.PlacedByLayer["place_city"].Count);

        // Re-render, order swapped (the merge order of two fresh tiles)
        var second = Compute(state, Layer(("place_city",
            [TextSymbol(0, 500, 500, envelope), TextSymbol(0, 100, 100, envelope)])));

        Assert.Equal(2, second.PlacedByLayer["place_city"].Count);
        Assert.Empty(second.Rejected);
    }

    /// <summary>
    /// Verifies that once the state has accumulated <see cref="IncrementalPlacementState.MaxDecisions" />
    /// decisions, the next pass starts clean and places everything again, including new symbols.
    /// </summary>
    [Fact]
    public void MaxDecisions_Exceeded_NextPassStartsClean()
    {
        // Fill the state up to the maximum (one symbol per tile, spread over
        // the world, so no collisions). Distinct ids, so every symbol is one
        // decision.
        var state = new IncrementalPlacementState();
        var tiles = new List<IRenderedTile>();

        for (ulong i = 1; i <= IncrementalPlacementState.MaxDecisions; i++)
        {
            tiles.Add(Layer(("place_low",
                [TextSymbol(i, 10_000 + 100 * i, 10_000, new Envelope(-10, 10, -10, 10))])));
        }

        var first = Compute(state, tiles.ToArray());
        Assert.Equal(IncrementalPlacementState.MaxDecisions, first.PlacedByLayer["place_low"].Count);

        // The next pass starts clean: everything is placed again, and the
        // additional symbol of the last tile is placed as well
        tiles.Add(Layer(("place_low",
            [TextSymbol(0, 100, 100, new Envelope(-10, 10, -10, 10))])));

        var second = Compute(state, tiles.ToArray());
        Assert.Equal(IncrementalPlacementState.MaxDecisions + 1,
            second.PlacedByLayer["place_low"].Count);
        Assert.Empty(second.Rejected);
    }

    /// <summary>
    /// Verifies that feature ids are only matched within a style layer: MVT/MLT
    /// feature ids are unique per layer only, so the same id in two layers is
    /// two different features. The decisions were previously kept by the bare
    /// id, so one layer's feature inherited the pinned decision of the other
    /// layer's feature with the same id and was never placed (MapLibre
    /// demotiles: the geolines labels share the ids 2, 3, 4 and 5 with the
    /// country labels of the same tile).
    /// </summary>
    [Fact]
    public void SameFeatureIdInDifferentLayers_IsPlacedInBothLayers()
    {
        var symbolA = TextSymbol(5, 100, 100, new Envelope(-10, 10, -10, 10));
        var symbolB = TextSymbol(5, 500, 500, new Envelope(-10, 10, -10, 10));
        var tile = Layer(("layer-a", [symbolA]), ("layer-b", [symbolB]));

        var state = new IncrementalPlacementState();

        var first = Compute(state, tile);
        Assert.Single(first.PlacedByLayer["layer-a"]);
        Assert.Single(first.PlacedByLayer["layer-b"]);
        Assert.Empty(first.Rejected);

        // The decision of each feature must also survive the next pass
        var second = Compute(state, tile);
        Assert.Single(second.PlacedByLayer["layer-a"]);
        Assert.Single(second.PlacedByLayer["layer-b"]);
        Assert.Empty(second.Rejected);
    }
}
