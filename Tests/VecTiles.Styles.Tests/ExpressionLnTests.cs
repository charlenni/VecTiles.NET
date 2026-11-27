using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// ln.a:
/// ["ln", 2]
/// ln.b: differs in the request of Euler's number.
/// </summary>
public class ExpressionLnTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("ln", "a");
        var expressionA2 = ExpressionFixture.Parse("ln", "a");

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
        var expressionA = ExpressionFixture.Parse("ln", "a");
        var expressionB = ExpressionFixture.Parse("ln", "b");

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
        Assert.Equal(0.6931471805599453, ExpressionFixture.Parse("ln", "a")!.Evaluate(ExpressionTestsHelper.CreateContext()));
        Assert.Equal(1d, ExpressionFixture.Parse("ln", "b")!.Evaluate(ExpressionTestsHelper.CreateContext()));
    }
}
