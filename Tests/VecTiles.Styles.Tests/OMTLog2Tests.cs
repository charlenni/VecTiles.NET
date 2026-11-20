using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// log2.a:
/// ["log2", 1024]
/// log2.b: differs in the number.
/// </summary>
public class OMTLog2Tests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("log2", "a");
        var expressionA2 = OMTExpressionFixture.Parse("log2", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("log2", "a");
        var expressionB = OMTExpressionFixture.Parse("log2", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        Assert.Equal(10d, OMTExpressionFixture.Parse("log2", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        Assert.Equal(10.001408194392809, (double)OMTExpressionFixture.Parse("log2", "b")!.Evaluate(OMTExpressionTestsHelper.CreateContext())!, 12);
    }
}
