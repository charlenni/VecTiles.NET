using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// at.a:
/// ["number", ["at", ["number", ["get", "i"]], ["array", ["get", "arr"]]]]
/// at.b: differs in the array attribute key.
/// </summary>
public class ExpressionAtTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("at", "a");
        var expressionA2 = ExpressionFixture.Parse("at", "a");

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
        var expressionA = ExpressionFixture.Parse("at", "a");
        var expressionB = ExpressionFixture.Parse("at", "b");

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
        var expression = ExpressionFixture.Parse("at", "a");

        Assert.NotNull(expression);

        var context = ExpressionTestsHelper.CreateContext(("i", 1d), ("arr", new object?[] { 10d, 20d, 30d }));

        Assert.Equal(20d, expression!.Evaluate(context));
    }

    /// <summary>
    /// Verifies that the at expression evaluates to null for an out-of-range index.
    /// </summary>
    [Fact]
    public void OutOfRangeIndexTest()
    {
        var expression = ExpressionFixture.Parse("at", "a");

        Assert.NotNull(expression);

        var context = ExpressionTestsHelper.CreateContext(("i", 7d), ("arr", new object?[] { 10d, 20d, 30d }));

        Assert.Null(expression!.Evaluate(context));
    }
}
