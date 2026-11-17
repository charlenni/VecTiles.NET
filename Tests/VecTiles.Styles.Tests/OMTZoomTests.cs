using Newtonsoft.Json.Linq;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// zoom.a:
/// ["interpolate", ["linear"], ["zoom"], 0, 0, 30, 30]
/// zoom.b: differs in the last output.
/// </summary>
public class OMTZoomTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("zoom", "a");
        var expressionA2 = OMTExpressionFixture.Parse("zoom", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("zoom", "a");
        var expressionB = OMTExpressionFixture.Parse("zoom", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        // The helper context uses zoom 14
        var expression = OMTExpressionFixture.Parse("zoom", "a");

        Assert.NotNull(expression);

        Assert.Equal(14d, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
