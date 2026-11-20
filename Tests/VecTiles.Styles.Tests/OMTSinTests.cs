using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// sin.a:
/// ["sin", 0]
/// sin.b: differs in the angle.
/// </summary>
public class OMTSinTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("sin", "a");
        var expressionA2 = OMTExpressionFixture.Parse("sin", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("sin", "a");
        var expressionB = OMTExpressionFixture.Parse("sin", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        Assert.Equal(0d, OMTExpressionFixture.Parse("sin", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        Assert.Equal(0.8414709848078965, OMTExpressionFixture.Parse("sin", "b")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
