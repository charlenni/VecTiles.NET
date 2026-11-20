using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// acos.a:
/// ["acos", 0.5]
/// acos.b: differs in the angle.
/// </summary>
public class OMTAcosTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("acos", "a");
        var expressionA2 = OMTExpressionFixture.Parse("acos", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("acos", "a");
        var expressionB = OMTExpressionFixture.Parse("acos", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expressionA = OMTExpressionFixture.Parse("acos", "a");
        var expressionB = OMTExpressionFixture.Parse("acos", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);

        Assert.Equal(1.0471975511965979, expressionA!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        Assert.Equal(double.NaN, expressionB!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
