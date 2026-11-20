using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// upcase.a:
/// ["upcase", "string"]
/// upcase.b: differs in the string.
/// </summary>
public class OMTUpcaseTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("upcase", "a");
        var expressionA2 = OMTExpressionFixture.Parse("upcase", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("upcase", "a");
        var expressionB = OMTExpressionFixture.Parse("upcase", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        Assert.Equal("STRING", OMTExpressionFixture.Parse("upcase", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
