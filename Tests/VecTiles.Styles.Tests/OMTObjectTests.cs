using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// object.a:
/// ["object", ["get", "x"]]
/// object.b: differs in the attribute key.
/// </summary>
public class OMTObjectTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("object", "a");
        var expressionA2 = OMTExpressionFixture.Parse("object", "a");

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
        var expressionA = OMTExpressionFixture.Parse("object", "a");
        var expressionB = OMTExpressionFixture.Parse("object", "b");

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
        var expression = OMTExpressionFixture.Parse("object", "a");

        Assert.NotNull(expression);

        var value = new Dictionary<string, object?> { ["key"] = "value" };

        Assert.Equal(value, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", value))));
    }

    /// <summary>
    /// Verifies that a non-object attribute value evaluates to null when no fallback is given.
    /// </summary>
    [Fact]
    public void NonObjectWithoutFallbackTest()
    {
        var expression = OMTExpressionFixture.Parse("object", "a");

        Assert.NotNull(expression);

        Assert.Null(expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", "value"))));
    }
}
