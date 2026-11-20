using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// string.a:
/// ["string", ["get", "x"]]
/// string.b: differs in the attribute key.
/// </summary>
public class OMTStringTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("string", "a");
        var expressionA2 = OMTExpressionFixture.Parse("string", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("string", "a");
        var expressionB = OMTExpressionFixture.Parse("string", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        Assert.Equal("value", OMTExpressionFixture.Parse("string", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", "value"))));
    }

    [Fact]
    public void NonStringWithoutFallbackTest()
    {
        Assert.Null(OMTExpressionFixture.Parse("string", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", 1d))));
    }
}
