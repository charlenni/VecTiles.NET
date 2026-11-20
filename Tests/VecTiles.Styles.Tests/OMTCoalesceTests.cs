using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// coalesce.a:
/// ["coalesce", ["get", "x"], ["get", "y"], ["get", "z"], 0]
/// coalesce.b: differs in the last fallback.
/// </summary>
public class OMTCoalesceTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("coalesce", "a");
        var expressionA2 = OMTExpressionFixture.Parse("coalesce", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("coalesce", "a");
        var expressionB = OMTExpressionFixture.Parse("coalesce", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expression = OMTExpressionFixture.Parse("coalesce", "a");

        Assert.NotNull(expression);

        Assert.Equal("y", expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("y", "y"))));
    }

    [Fact]
    public void FallsBackToTheLastExpressionTest()
    {
        var expression = OMTExpressionFixture.Parse("coalesce", "a");

        Assert.NotNull(expression);

        Assert.Equal(0d, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
