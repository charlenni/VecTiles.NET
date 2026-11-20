using VecTiles.Common.Primitives;
using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// rgb.a:
/// ["rgb", 0, 0, 255]
/// rgb.b: differs in the blue channel.
/// </summary>
public class OMTRgbTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("rgb", "a");
        var expressionA2 = OMTExpressionFixture.Parse("rgb", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("rgb", "a");
        var expressionB = OMTExpressionFixture.Parse("rgb", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var color = Assert.IsType<Color>(OMTExpressionFixture.Parse("rgb", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));

        Assert.Equal(new Color(0, 0, 255, 255), color);
    }
}
