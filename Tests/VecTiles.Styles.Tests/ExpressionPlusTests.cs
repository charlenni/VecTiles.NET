using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// plus.a:
/// ["+", 1, 2, 3, 4]
/// plus.b: differs in the last addend.
/// </summary>
public class ExpressionPlusTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("plus", "a");
        var expressionA2 = ExpressionFixture.Parse("plus", "a");

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
        var expressionA = ExpressionFixture.Parse("plus", "a");
        var expressionB = ExpressionFixture.Parse("plus", "b");

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
        Assert.Equal(10d, ExpressionFixture.Parse("plus", "a")!.Evaluate(ExpressionTestsHelper.CreateContext()));
        Assert.Equal(11d, ExpressionFixture.Parse("plus", "b")!.Evaluate(ExpressionTestsHelper.CreateContext()));
    }
}
