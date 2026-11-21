using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native "let" fixtures
/// (test/fixtures/expression_equality/let.json):
/// let.a: ["let", "a", 1, "b", 2, ["+", ["+", ["var", "a"], ["var", "b"]], ["var", "a"]]]
/// let.b: differs in the binding values ("b": 2 vs. 3) and the result branches.
/// The "+" operation isn't implemented yet, so the fixtures are reduced to
/// literal bindings with a "var" result.
/// </summary>
public class OMTExpressionLetTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionParser.ParseExpression(JsonNode.Parse(
            """
            ["let", "a", 1, "b", 2, ["var", "a"]]
            """));
        var expressionA2 = ExpressionParser.ParseExpression(JsonNode.Parse(
            """
            ["let", "a", 1, "b", 2, ["var", "a"]]
            """));

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
        // The leaf difference of the fixture: the binding "b" is 2 vs. 3
        var expressionA = ExpressionParser.ParseExpression(JsonNode.Parse(
            """
            ["let", "a", 1, "b", 2, ["var", "a"]]
            """));
        var expressionB = ExpressionParser.ParseExpression(JsonNode.Parse(
            """
            ["let", "a", 1, "b", 3, ["var", "a"]]
            """));

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
        var expressionA = ExpressionParser.ParseExpression(JsonNode.Parse(
            """
            ["let", "a", 1, "b", 2, ["var", "a"]]
            """));

        Assert.NotNull(expressionA);
        Assert.Equal(1d, expressionA!.Evaluate(OMTExpressionTestsHelper.CreateContext()));

        // Shadowing: the inner binding wins
        var expressionShadow = ExpressionParser.ParseExpression(JsonNode.Parse(
            """
            ["let", "a", 1, ["let", "a", 2, ["var", "a"]]]
            """));

        Assert.NotNull(expressionShadow);
        Assert.Equal(2d, expressionShadow.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
