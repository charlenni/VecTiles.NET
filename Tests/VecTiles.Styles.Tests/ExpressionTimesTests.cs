using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// times.a:
/// ["*", 3, 2, 0.5, 2]
/// times.b: differs in the last factor.
/// </summary>
public class ExpressionTimesTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("times", "a");
        var expressionA2 = ExpressionFixture.Parse("times", "a");

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
        var expressionA = ExpressionFixture.Parse("times", "a");
        var expressionB = ExpressionFixture.Parse("times", "b");

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
        Assert.Equal(6d, ExpressionFixture.Parse("times", "a")!.Evaluate(ExpressionTestsHelper.CreateContext()));
        Assert.Equal(9d, ExpressionFixture.Parse("times", "b")!.Evaluate(ExpressionTestsHelper.CreateContext()));
    }
}
