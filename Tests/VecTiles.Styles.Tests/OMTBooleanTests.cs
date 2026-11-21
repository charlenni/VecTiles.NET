using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// boolean.a:
/// ["boolean", ["get", "x"]]
/// boolean.b: differs in the attribute key.
/// </summary>
public class OMTBooleanTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("boolean", "a");
        var expressionA2 = OMTExpressionFixture.Parse("boolean", "a");

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
        var expressionA = OMTExpressionFixture.Parse("boolean", "a");
        var expressionB = OMTExpressionFixture.Parse("boolean", "b");

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
        var expression = OMTExpressionFixture.Parse("boolean", "a");

        Assert.NotNull(expression);

        Assert.Equal(true, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", true))));
    }

    /// <summary>
    /// Verifies that a non-boolean attribute value evaluates to null when no fallback is given.
    /// </summary>
    [Fact]
    public void NonBooleanWithoutFallbackTest()
    {
        var expression = OMTExpressionFixture.Parse("boolean", "a");

        Assert.NotNull(expression);

        // Numbers are not booleans and there is no fallback
        Assert.Null(expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", 1d))));
    }

    /// <summary>
    /// Verifies that a missing attribute evaluates to null when no fallback is given.
    /// </summary>
    [Fact]
    public void MissingAttributeWithoutFallbackTest()
    {
        var expression = OMTExpressionFixture.Parse("boolean", "a");

        Assert.NotNull(expression);

        Assert.Null(expression!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
