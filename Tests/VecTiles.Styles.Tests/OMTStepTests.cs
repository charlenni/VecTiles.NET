using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// step.a:
/// ["number", ["step", ["number", ["get", "x"]], 11, 0, 111, 1, 1111]]
/// step.b: differs in the last output.
/// </summary>
public class OMTStepTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("step", "a");
        var expressionA2 = OMTExpressionFixture.Parse("step", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("step", "a");
        var expressionB = OMTExpressionFixture.Parse("step", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Theory]
    [InlineData(-1d, 11d)]
    [InlineData(0d, 111d)]
    [InlineData(0.5d, 111d)]
    [InlineData(1d, 1111d)]
    [InlineData(2d, 1111d)]
    public void FixtureEvaluationTest(double x, double expected)
    {
        var expression = OMTExpressionFixture.Parse("step", "a");

        Assert.NotNull(expression);

        var context = OMTExpressionTestsHelper.CreateContext(("x", x));

        Assert.Equal(expected, expression!.Evaluate(context));
    }
}
