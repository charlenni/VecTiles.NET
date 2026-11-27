using System.Text.Json;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles;
using VecTiles.Styles.OpenMapTiles.Json;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests of the line-round-limit wiring of the OMT line paint: the style
/// value must reach <see cref="Paint.StrokeRoundLimit"/> (the renderer passes
/// it to the line tessellation, which converts round joins to miter joins at
/// shallow angles when the miter ratio stays below the limit). Guards against
/// the defect where line-round-limit was parsed from the style but never
/// applied.
/// </summary>
public class OMTLinePaintRoundLimitTests
{
    private static Paint CreatePaint(string layerJson)
    {
        var file = JsonSerializer.Deserialize<OMTStyleFile>(
            $$"""{"layers":[{{layerJson}}]}""", StyleJson.Options);

        Assert.NotNull(file);
        return OMTLinePaint.CreatePaint(file!.Layers[0], _ => null!);
    }

    [Fact]
    public void DefaultRoundLimitIsOnePointZeroFiveTest()
    {
        var paint = CreatePaint("""{"id":"road","type":"line","paint":{"line-color":"#f00"}}""");

        paint.Update(new EvaluationContext(10f));

        // Mapbox style spec: line-round-limit defaults to 1.05
        Assert.Equal(1.05f, paint.StrokeRoundLimit);
    }

    [Fact]
    public void ConstantRoundLimitTest()
    {
        var paint = CreatePaint("""
            {"id":"road","type":"line",
             "layout":{"line-round-limit":2.0},
             "paint":{}}
            """);

        paint.Update(new EvaluationContext(10f));

        Assert.Equal(2f, paint.StrokeRoundLimit);
    }

    [Fact]
    public void VariableRoundLimitFollowsZoomTest()
    {
        var paint = CreatePaint("""
            {"id":"road","type":"line",
             "layout":{"line-round-limit":{"stops":[[10,1],[16,2]]}},
             "paint":{}}
            """);

        paint.Update(new EvaluationContext(10f));
        Assert.Equal(1f, paint.StrokeRoundLimit);

        paint.Update(new EvaluationContext(13f));
        Assert.Equal(1.5f, paint.StrokeRoundLimit);
    }
}
