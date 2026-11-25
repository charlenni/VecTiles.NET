using System.Text.Json;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles;
using VecTiles.Styles.OpenMapTiles.Json;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Regression tests of the dash array conversion of the OMT line paint
/// (Mapbox semantics: dash and gap lengths are multiples of the line width).
/// Guards against the defect where the constant dash array of a layer with a
/// zoom-interpolated line width was scaled once with the line width at zoom 0
/// (EvaluationContext.Empty), so the pattern was far too narrow at every zoom
/// above 0. The pattern must scale with the line width frozen at the integer
/// zoom of the evaluation context (like the variable dash array does).
/// </summary>
public class OMTLinePaintDashTests
{
    private static Paint CreatePaint(string layerJson)
    {
        var file = JsonSerializer.Deserialize<OMTStyleFile>(
            $$"""{"layers":[{{layerJson}}]}""", StyleJson.Options);

        Assert.NotNull(file);
        return OMTLinePaint.CreatePaint(file!.Layers[0], _ => null!);
    }

    [Fact]
    public void ConstantDashArrayScalesWithLineWidthFrozenAtIntZoomTest()
    {
        var paint = CreatePaint("""
            {"id":"rail","type":"line",
             "paint":{
                 "line-width":{"stops":[[10,0.5],[16,4]]},
                 "line-dasharray":[2,2]}}
            """);

        // Zoom 16.2: the line width evaluates to 4 at the integer zoom 16,
        // so the dash/gap lengths are 2 * 4 = 8 (half the interpolated width
        // between 16 and 17 would be wrong, the zoom 0 width of 0.5 was the
        // defect: dashes of 1)
        paint.Update(new EvaluationContext(16.2f));
        Assert.Equal(new[] { 8f, 8f }, paint.DashArray);
    }

    [Fact]
    public void DashArrayFollowsTheContextScaleTest()
    {
        var paint = CreatePaint("""
            {"id":"rail","type":"line",
             "paint":{
                 "line-width":{"stops":[[10,0.5],[16,4]]},
                 "line-dasharray":[2,2]}}
            """);

        // Zoom 16.2 doubled by the scale 2: the dash lengths multiply with
        // the stroke width the paint produces (width * scale = 8)
        paint.Update(new EvaluationContext(16.2f, 2f));
        Assert.Equal(new[] { 16f, 16f }, paint.DashArray);
    }

    [Fact]
    public void VariableDashArrayScalesWithLineWidthFrozenAtIntZoomTest()
    {
        var paint = CreatePaint("""
            {"id":"rail","type":"line",
             "paint":{
                 "line-width":{"stops":[[10,1],[16,4]]},
                 "line-dasharray":{"stops":[[10,[1,1]],[16,[2,4]]]}}}
            """);

        paint.Update(new EvaluationContext(16.5f));
        Assert.Equal(new[] { 8f, 16f }, paint.DashArray);
    }

    [Fact]
    public void ConstantDashArrayOfConstantWidthTest()
    {
        var paint = CreatePaint("""
            {"id":"rail","type":"line",
             "paint":{
                 "line-width":2,
                 "line-dasharray":[2,2]}}
            """);

        paint.Update(new EvaluationContext(14));
        Assert.Equal(new[] { 4f, 4f }, paint.DashArray);
    }
}
