using VecTiles.Common.Primitives;
using Newtonsoft.Json.Linq;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// heatmap-density.a:
/// ["interpolate", ["linear"], ["heatmap-density"], 0, ["rgb", 0, 0, 255], 1, ["rgb", 255, 0, 0]]
/// heatmap-density.b: differs in the last output color.
/// </summary>
public class OMTHeatmapDensityTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("heatmap-density", "a");
        var expressionA2 = OMTExpressionFixture.Parse("heatmap-density", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("heatmap-density", "a");
        var expressionB = OMTExpressionFixture.Parse("heatmap-density", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void DensityZeroIsTheFirstColorTest()
    {
        // The heatmap density isn't supplied by the renderer and evaluates to 0
        var expressionA = OMTExpressionFixture.Parse("heatmap-density", "a");
        var expressionB = OMTExpressionFixture.Parse("heatmap-density", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);

        Assert.Equal(new Color(0, 0, 255, 255), Assert.IsType<Color>(expressionA!.Evaluate(OMTExpressionTestsHelper.CreateContext())));
        Assert.Equal(new Color(0, 0, 255, 255), Assert.IsType<Color>(expressionB!.Evaluate(OMTExpressionTestsHelper.CreateContext())));
    }
}
