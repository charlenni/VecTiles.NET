using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// semiliteral.a:
/// ["semiliteral", [["get", "x"], ["get", "y"]]]
/// semiliteral.b: differs in the order of the elements.
/// </summary>
public class OMTSemiliteralTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("semiliteral", "a");
        var expressionA2 = OMTExpressionFixture.Parse("semiliteral", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("semiliteral", "a");
        var expressionB = OMTExpressionFixture.Parse("semiliteral", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expressionA = OMTExpressionFixture.Parse("semiliteral", "a");
        var expressionB = OMTExpressionFixture.Parse("semiliteral", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);

        Assert.Equal(new object?[] { "a", "b" }, expressionA!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", "a"), ("y", "b"))));
        Assert.Equal(new object?[] { "b", "a" }, expressionB!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", "a"), ("y", "b"))));
    }
}
