using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// log10.a:
/// ["log10", 100]
/// log10.b: differs in the number.
/// </summary>
public class OMTLog10Tests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("log10", "a");
        var expressionA2 = OMTExpressionFixture.Parse("log10", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("log10", "a");
        var expressionB = OMTExpressionFixture.Parse("log10", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        Assert.Equal(2d, OMTExpressionFixture.Parse("log10", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        Assert.Equal(2.0043213737826426, OMTExpressionFixture.Parse("log10", "b")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
