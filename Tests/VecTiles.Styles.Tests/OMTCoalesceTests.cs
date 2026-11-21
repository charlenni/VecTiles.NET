using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// coalesce.a:
/// ["coalesce", ["get", "x"], ["get", "y"], ["get", "z"], 0]
/// coalesce.b: differs in the last fallback.
/// </summary>
public class OMTCoalesceTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("coalesce", "a");
        var expressionA2 = OMTExpressionFixture.Parse("coalesce", "a");

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
        var expressionA = OMTExpressionFixture.Parse("coalesce", "a");
        var expressionB = OMTExpressionFixture.Parse("coalesce", "b");

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
        var expression = OMTExpressionFixture.Parse("coalesce", "a");

        Assert.NotNull(expression);

        Assert.Equal("y", expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("y", "y"))));
    }

    /// <summary>
    /// Verifies that the coalesce expression falls back to its last operand when all get expressions evaluate to null.
    /// </summary>
    [Fact]
    public void FallsBackToTheLastExpressionTest()
    {
        var expression = OMTExpressionFixture.Parse("coalesce", "a");

        Assert.NotNull(expression);

        Assert.Equal(0d, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
