using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// any.a:
/// ["any", ["boolean", ["get", "x"]], ["boolean", ["get", "y"]]]
/// any.b: differs in the second attribute key.
/// </summary>
public class OMTAnyTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("any", "a");
        var expressionA2 = OMTExpressionFixture.Parse("any", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("any", "a");
        var expressionB = OMTExpressionFixture.Parse("any", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    public void FixtureEvaluationTest(bool x, bool y, bool expected)
    {
        var expression = OMTExpressionFixture.Parse("any", "a");

        Assert.NotNull(expression);

        var context = OMTExpressionTestsHelper.CreateContext(("x", x), ("y", y));

        Assert.Equal(expected, expression!.Evaluate(context));
    }

    [Fact]
    public void MissingAttributeIsFalsyTest()
    {
        var expression = OMTExpressionFixture.Parse("any", "a");

        Assert.NotNull(expression);

        // All conditions fail on the missing attribute, so the result is falsy
        Assert.False((bool)expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", false)))!);
    }
}
