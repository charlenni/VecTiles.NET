using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// concat.a:
/// ["concat", "a", "b", "c"]
/// concat.b: differs in the last part.
/// </summary>
public class OMTConcatTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("concat", "a");
        var expressionA2 = OMTExpressionFixture.Parse("concat", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("concat", "a");
        var expressionB = OMTExpressionFixture.Parse("concat", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expression = OMTExpressionFixture.Parse("concat", "a");

        Assert.NotNull(expression);

        Assert.Equal("abc", expression!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
