using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// min.a:
/// ["min", ["get", "x"], 0]
/// min.b: differs in the second number.
/// </summary>
public class OMTMinTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("min", "a");
        var expressionA2 = OMTExpressionFixture.Parse("min", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("min", "a");
        var expressionB = OMTExpressionFixture.Parse("min", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expression = OMTExpressionFixture.Parse("min", "a");

        Assert.NotNull(expression);

        Assert.Equal(-5d, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", -5d))));
    }

    [Fact]
    public void MissingAttributeTest()
    {
        var expression = OMTExpressionFixture.Parse("min", "a");

        Assert.NotNull(expression);

        // A missing attribute can't be compared numerically
        Assert.Null(expression!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
