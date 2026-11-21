using NetTopologySuite.Geometries;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Skia;
using VecTiles.Renderers.Common;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Unit tests for the persistent placement decisions (IncrementalPlacementState):
/// a pass with a state must place exactly like a pass without one, and the
/// decisions (placed position, rejection) must survive the following passes,
/// also when the tile set of the viewport changes.
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

    [Fact]
    public void SecondPass_RejectedStaysRejected_WhenSpaceFreesUp()
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
        // rejection stands — the label does not jump in afterwards. The
        // rejection is kept in the state; the per-pass rejected list follows
        // the examined symbols of the pass, so the label simply stays absent
        var second = Compute(state, Layer(("place_low", [low])));

        Assert.Empty(second.PlacedByLayer["place_low"]);
        Assert.Empty(second.Rejected);
    }

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
}
