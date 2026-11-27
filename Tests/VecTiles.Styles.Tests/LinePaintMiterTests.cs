using System.Text.Json;
using VecTiles.Common.Primitives;
using VecTiles.Styles.MapLibre;
using VecTiles.Styles.MapLibre.Json;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests of the line-miter-limit wiring of the OMT line paint: the style
/// value must reach <see cref="Paint.StrokeMiter"/> (the renderer passes it
/// to the line tessellation, which falls back to Bevel joins when the miter
/// ratio exceeds the limit). Guards against the defect where
/// line-miter-limit was parsed from the style but never applied, so sharp
/// road corners produced extreme miter spikes.
/// </summary>
public class LinePaintMiterTests
{
    private static Paint CreatePaint(string layerJson)
    {
        var file = JsonSerializer.Deserialize<StyleFile>(
            $$"""{"layers":[{{layerJson}}]}""", StyleJson.Options);

        Assert.NotNull(file);
        return LinePaint.CreatePaint(file!.Layers[0], _ => null!);
    }

    [Fact]
    public void DefaultMiterLimitIsTwoTest()
    {
        var paint = CreatePaint("""{"id":"road","type":"line","paint":{"line-color":"#f00"}}""");

        paint.Update(new EvaluationContext(10f));

        // Mapbox style spec: line-miter-limit defaults to 2
        Assert.Equal(2f, paint.StrokeMiter);
    }

    [Fact]
    public void ConstantMiterLimitTest()
    {
        var paint = CreatePaint("""
            {"id":"road","type":"line",
             "layout":{"line-miter-limit":0.5},
             "paint":{}}
            """);

        paint.Update(new EvaluationContext(10f));

        Assert.Equal(0.5f, paint.StrokeMiter);
    }

    [Fact]
    public void VariableMiterLimitFollowsZoomTest()
    {
        var paint = CreatePaint("""
            {"id":"road","type":"line",
             "layout":{"line-miter-limit":{"stops":[[10,2],[16,4]]}},
             "paint":{}}
            """);

        paint.Update(new EvaluationContext(10f));
        Assert.Equal(2f, paint.StrokeMiter);

        paint.Update(new EvaluationContext(13f));
        Assert.Equal(3f, paint.StrokeMiter);
    }
}
