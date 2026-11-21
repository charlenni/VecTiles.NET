using NetTopologySuite.Geometries;
using VecTiles.Common.Primitives;
using VecTiles.Controls.Mapsui;
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
        var feature = new RenderedTileFeature(tile, 0, 0, 1000, 1000);

        return SymbolPlacementEngine.Compute([feature], ZoomLevel, WorldToScreen);
    }

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
}
