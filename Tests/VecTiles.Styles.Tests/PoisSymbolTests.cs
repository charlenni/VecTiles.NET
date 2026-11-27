using System.Text.Json;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Extensions;
using VecTiles.Common.Primitives;
using VecTiles.Styles.MapLibre;
using VecTiles.Styles.MapLibre.Json;
using VecTiles.Tests;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests for the symbol properties of the shortbread based Protomaps style
/// (Files/Protomaps/protomaps-light-style.json), which uses feature-dependent
/// text colors and text-variable-anchor placement for its POI labels.
/// </summary>
public class PoisSymbolTests
{
    private static string StylePath => Path.Combine(RepoData.ProtomapsFiles, "protomaps-light-style.json");

    private static TextPointSymbol BuildPoisText(string kind, float zoom)
    {
        var json = File.ReadAllText(StylePath);
        var styleFile = JsonSerializer.Deserialize<StyleFile>(json, StyleJson.Options)!;
        var pois = styleFile.Layers.First(l => l.Name == "pois");

        var feature = new Feature(new Point(100, 100),
            new AttributesTable(new Dictionary<string, object?> { ["kind"] = kind, ["name"] = "Cafe" }));

        var buildContext = new EvaluationContext(zoom).WithFeature(feature);

        return new TextBuilder(pois, buildContext)
            .Build(new Tile(8580, 5734, 14), new Point(0, 0), feature)!;
    }

    [Fact]
    public void DataDrivenTextColorIsEvaluatedWithFeature()
    {
        var symbol = BuildPoisText("restaurant", 17);

        // The draw context the renderer creates has no feature; the color of
        // the pois layer is a case on "kind" and must not fall back to the
        // default branch (#e2dfda, the background color)
        var drawColor = symbol.Color!(new EvaluationContext(17));

        // Restaurant text color of the style is #CB6704
        Assert.Equal((byte)0xCB, drawColor.R);
        Assert.Equal((byte)0x67, drawColor.G);
        Assert.Equal((byte)0x04, drawColor.B);
    }

    [Fact]
    public void DataDrivenTextColorIsZoomDependentWithFeature()
    {
        var symbol = BuildPoisText("restaurant", 17);

        // The zoom of the draw context must still be respected
        var drawColor = symbol.Color!(new EvaluationContext(18));

        Assert.Equal((byte)0xCB, drawColor.R);
    }

    [Fact]
    public void ConstantTextColorStaysConstant()
    {
        var symbol = BuildPoisText("restaurant", 17);

        // text-halo-color of the pois layer is the constant #e2dfda
        var halo = symbol.HaloColor!(new EvaluationContext(17));

        Assert.Equal((byte)0xE2, halo.R);
        Assert.Equal((byte)0xDF, halo.G);
        Assert.Equal((byte)0xDA, halo.B);
    }

    [Fact]
    public void VariableAnchorsCarryTheRadialDistance()
    {
        // The pois layer: text-variable-anchor ["left", "right"],
        // text-offset [1.1, 0] em, text-size 10 at zoom 17
        var symbol = BuildPoisText("restaurant", 17);

        Assert.Equal(2, symbol.VariableAnchors.Length);
        Assert.Equal(Anchor.Left, symbol.VariableAnchors[0]);
        Assert.Equal(Anchor.Right, symbol.VariableAnchors[1]);

        // The offsets push the text 1.1 em away from the point, to the right
        // for the left anchor and to the left for the right anchor
        Assert.Equal(11f, symbol.VariableOffsets[0].X, 2f);
        Assert.Equal(0f, symbol.VariableOffsets[0].Y, 2f);
        Assert.Equal(-11f, symbol.VariableOffsets[1].X, 2f);
        Assert.Equal(0f, symbol.VariableOffsets[1].Y, 2f);

        // The initial placement is the first candidate anchor
        Assert.Equal(symbol.VariableAnchors[0].ToPoint(), symbol.Anchor);
        Assert.Equal(symbol.VariableOffsets[0], symbol.Offset);
    }

    [Fact]
    public void RadialOffsetIsUsedAsDistance()
    {
        // places_locality: text-radial-offset 0.3 em, text-size 8 at zoom 2
        var json = File.ReadAllText(StylePath);
        var styleFile = JsonSerializer.Deserialize<StyleFile>(json, StyleJson.Options)!;
        var places = styleFile.Layers.First(l => l.Name == "places_locality");

        var feature = new Feature(new Point(100, 100),
            new AttributesTable(new Dictionary<string, object?>
            {
                ["kind"] = "locality",
                ["name"] = "Zurich",
                ["population_rank"] = 8,
            }));

        var buildContext = new EvaluationContext(2).WithFeature(feature);

        var symbol = new TextBuilder(places, buildContext)
            .Build(new Tile(0, 0, 2), new Point(0, 0), feature)!;

        // The variable anchor of places_locality is a step expression
        // (["bottom", "left", "right", "top"] below zoom 8)
        Assert.Equal(4, symbol.VariableAnchors.Length);
        Assert.Equal(Anchor.Bottom, symbol.VariableAnchors[0]);

        // text-size at zoom 2 for population_rank < 13 is 8; the bottom
        // anchor pushes the text 0.3 em upwards (negative screen y)
        Assert.Equal(-2.4f, symbol.VariableOffsets[0].Y, 1f);
    }

    [Fact]
    public void JustifyAutoAlignsTowardsTheChosenAnchor()
    {
        // The pois layer sets text-justify auto; the text is aligned towards
        // the anchor position: left for the left anchor, right for the right
        var symbol = BuildPoisText("restaurant", 17);

        Assert.True(symbol.JustifyAuto);
        Assert.Equal(TextJustify.Left, symbol.Alignment);

        // Placement chooses the right anchor: the text flips to the left of
        // the icon and becomes right aligned
        symbol.UsePlacement(Anchor.Right, symbol.VariableOffsets[1]);

        Assert.Equal(Anchor.Right.ToPoint(), symbol.Anchor);
        Assert.Equal(TextJustify.Right, symbol.Alignment);

        // Placement chooses the left anchor: left aligned again
        symbol.UsePlacement(Anchor.Left, symbol.VariableOffsets[0]);

        Assert.Equal(TextJustify.Left, symbol.Alignment);
    }
}
