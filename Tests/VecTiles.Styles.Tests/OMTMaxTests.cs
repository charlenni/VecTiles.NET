using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// max.a:
/// ["max", 0, -1, 100]
/// max.b: differs in the last number.
/// </summary>
public class OMTMaxTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("max", "a");
        var expressionA2 = OMTExpressionFixture.Parse("max", "a");

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
        var expressionA = OMTExpressionFixture.Parse("max", "a");
        var expressionB = OMTExpressionFixture.Parse("max", "b");

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
        Assert.Equal(100d, OMTExpressionFixture.Parse("max", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        Assert.Equal(101d, OMTExpressionFixture.Parse("max", "b")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
