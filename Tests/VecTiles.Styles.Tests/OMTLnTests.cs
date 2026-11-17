using Newtonsoft.Json.Linq;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// ln.a:
/// ["ln", 2]
/// ln.b: differs in the request of Euler's number.
/// </summary>
public class OMTLnTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("ln", "a");
        var expressionA2 = OMTExpressionFixture.Parse("ln", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("ln", "a");
        var expressionB = OMTExpressionFixture.Parse("ln", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        Assert.Equal(0.6931471805599453, OMTExpressionFixture.Parse("ln", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        Assert.Equal(1d, OMTExpressionFixture.Parse("ln", "b")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
