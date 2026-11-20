using VecTiles.Common.Primitives;
using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// to-color.a:
/// ["to-color", ["get", "x"]]
/// to-color.b: differs in the attribute key.
/// </summary>
public class OMTToColorTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("to-color", "a");
        var expressionA2 = OMTExpressionFixture.Parse("to-color", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("to-color", "a");
        var expressionB = OMTExpressionFixture.Parse("to-color", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void StringColorTest()
    {
        var expression = OMTExpressionFixture.Parse("to-color", "a");

        Assert.NotNull(expression);

        var color = Assert.IsType<Color>(expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", "rgb(255, 0, 0)"))));

        Assert.Equal(new Color(255, 0, 0, 255), color);
    }

    [Fact]
    public void NonColorWithoutFallbackTest()
    {
        var expression = OMTExpressionFixture.Parse("to-color", "a");

        Assert.NotNull(expression);

        Assert.Null(expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", 1d))));
    }
}
