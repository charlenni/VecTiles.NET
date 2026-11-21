using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// cos.a:
/// ["cos", 0]
/// cos.b: differs in the angle.
/// </summary>
public class OMTCosTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("cos", "a");
        var expressionA2 = OMTExpressionFixture.Parse("cos", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    /// <summary>
    /// Verifies that fixture a is not structurally equal to fixture b, which differs in exactly one leaf.
    /// </summary>
    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("cos", "a");
        var expressionB = OMTExpressionFixture.Parse("cos", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    /// <summary>
    /// Verifies that the fixtures evaluate to the expected results.
    /// </summary>
    [Fact]
    public void FixtureEvaluationTest()
    {
        var expressionA = OMTExpressionFixture.Parse("cos", "a");
        var expressionB = OMTExpressionFixture.Parse("cos", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);

        Assert.Equal(1d, expressionA!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        Assert.Equal(0.5403023058681398, expressionB!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
