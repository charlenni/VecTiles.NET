using VecTiles.Common.Primitives;
using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// heatmap-density.a:
/// ["interpolate", ["linear"], ["heatmap-density"], 0, ["rgb", 0, 0, 255], 1, ["rgb", 255, 0, 0]]
/// heatmap-density.b: differs in the last output color.
/// </summary>
public class ExpressionHeatmapDensityTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("heatmap-density", "a");
        var expressionA2 = ExpressionFixture.Parse("heatmap-density", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    /// <summary>
    /// Verifies that fixture a is not structurally equal to fixture b, which differs in exactly one leaf.
    /// </summary>
    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = ExpressionFixture.Parse("heatmap-density", "a");
        var expressionB = ExpressionFixture.Parse("heatmap-density", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    /// <summary>
    /// Verifies that the heatmap density of 0 evaluates to the first stop color for both fixtures.
    /// </summary>
    [Fact]
    public void DensityZeroIsTheFirstColorTest()
    {
        // The heatmap density isn't supplied by the renderer and evaluates to 0
        var expressionA = ExpressionFixture.Parse("heatmap-density", "a");
        var expressionB = ExpressionFixture.Parse("heatmap-density", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);

        Assert.Equal(new Color(0, 0, 255, 255), Assert.IsType<Color>(expressionA!.Evaluate(ExpressionTestsHelper.CreateContext())));
        Assert.Equal(new Color(0, 0, 255, 255), Assert.IsType<Color>(expressionB!.Evaluate(ExpressionTestsHelper.CreateContext())));
    }
}
