using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// plus.a:
/// ["+", 1, 2, 3, 4]
/// plus.b: differs in the last addend.
/// </summary>
public class OMTPlusTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("plus", "a");
        var expressionA2 = OMTExpressionFixture.Parse("plus", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("plus", "a");
        var expressionB = OMTExpressionFixture.Parse("plus", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        Assert.Equal(10d, OMTExpressionFixture.Parse("plus", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        Assert.Equal(11d, OMTExpressionFixture.Parse("plus", "b")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
