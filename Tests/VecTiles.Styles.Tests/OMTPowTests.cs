using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// pow.a:
/// ["^", 4, ["number", ["get", "x"]]]
/// pow.b: differs in the attribute key.
/// </summary>
public class OMTPowTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("pow", "a");
        var expressionA2 = OMTExpressionFixture.Parse("pow", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("pow", "a");
        var expressionB = OMTExpressionFixture.Parse("pow", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expression = OMTExpressionFixture.Parse("pow", "a");

        Assert.NotNull(expression);

        Assert.Equal(64d, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", 3d))));
    }
}
