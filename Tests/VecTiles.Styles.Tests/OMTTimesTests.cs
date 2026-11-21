using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// times.a:
/// ["*", 3, 2, 0.5, 2]
/// times.b: differs in the last factor.
/// </summary>
public class OMTTimesTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("times", "a");
        var expressionA2 = OMTExpressionFixture.Parse("times", "a");

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
        var expressionA = OMTExpressionFixture.Parse("times", "a");
        var expressionB = OMTExpressionFixture.Parse("times", "b");

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
        Assert.Equal(6d, OMTExpressionFixture.Parse("times", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        Assert.Equal(9d, OMTExpressionFixture.Parse("times", "b")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
