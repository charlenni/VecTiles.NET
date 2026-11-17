using Newtonsoft.Json.Linq;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// mod.a:
/// ["%", 18, 12]
/// mod.b: differs in the divisor.
/// </summary>
public class OMTModTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("mod", "a");
        var expressionA2 = OMTExpressionFixture.Parse("mod", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("mod", "a");
        var expressionB = OMTExpressionFixture.Parse("mod", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        Assert.Equal(6d, OMTExpressionFixture.Parse("mod", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        Assert.Equal(5d, OMTExpressionFixture.Parse("mod", "b")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
