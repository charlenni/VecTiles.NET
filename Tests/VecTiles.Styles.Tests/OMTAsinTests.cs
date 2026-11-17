using Newtonsoft.Json.Linq;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// asin.a:
/// ["asin", 0.5]
/// asin.b: differs in the angle.
/// </summary>
public class OMTAsinTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("asin", "a");
        var expressionA2 = OMTExpressionFixture.Parse("asin", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("asin", "a");
        var expressionB = OMTExpressionFixture.Parse("asin", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expressionA = OMTExpressionFixture.Parse("asin", "a");
        var expressionB = OMTExpressionFixture.Parse("asin", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);

        Assert.Equal(0.5235987755982989, expressionA!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        Assert.Equal(double.NaN, expressionB!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
