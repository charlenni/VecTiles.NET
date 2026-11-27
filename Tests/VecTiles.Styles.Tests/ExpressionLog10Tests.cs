using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// log10.a:
/// ["log10", 100]
/// log10.b: differs in the number.
/// </summary>
public class ExpressionLog10Tests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("log10", "a");
        var expressionA2 = ExpressionFixture.Parse("log10", "a");

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
        var expressionA = ExpressionFixture.Parse("log10", "a");
        var expressionB = ExpressionFixture.Parse("log10", "b");

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
        Assert.Equal(2d, ExpressionFixture.Parse("log10", "a")!.Evaluate(ExpressionTestsHelper.CreateContext()));
        Assert.Equal(2.0043213737826426, ExpressionFixture.Parse("log10", "b")!.Evaluate(ExpressionTestsHelper.CreateContext()));
    }
}
