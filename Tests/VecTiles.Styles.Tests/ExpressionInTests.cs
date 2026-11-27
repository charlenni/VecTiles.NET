using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// in.a:
/// ["number", ["in", ["number", ["get", "i"]], ["array", ["get", "arr"]]]]
/// in.b: differs in the array attribute key.
/// </summary>
public class ExpressionInTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("in", "a");
        var expressionA2 = ExpressionFixture.Parse("in", "a");

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
        var expressionA = ExpressionFixture.Parse("in", "a");
        var expressionB = ExpressionFixture.Parse("in", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    /// <summary>
    /// Verifies that the number operand is contained in the array attribute.
    /// </summary>
    /// <param name="i">The value of the i attribute.</param>
    /// <param name="expected">The expected membership result.</param>
    [Theory]
    [InlineData(20d, true)]
    [InlineData(1d, false)]
    public void MembershipTest(double i, bool expected)
    {
        // Membership without the wrapping number assertion of the fixture
        var expression = ExpressionParser.ParseExpression(JsonNode.Parse(
            """
            ["in", ["number", ["get", "i"]], ["array", ["get", "arr"]]]
            """));

        Assert.NotNull(expression);

        var context = ExpressionTestsHelper.CreateContext(("i", i), ("arr", new object?[] { 10d, 20d, 30d }));

        Assert.Equal(expected, expression!.Evaluate(context));
    }

    /// <summary>
    /// Verifies that membership in a large literal array haystack evaluates correctly.
    /// </summary>
    [Fact]
    public void LargeHaystackUsesHashSetTest()
    {
        var expression = ExpressionParser.ParseExpression(JsonNode.Parse(
            """
            ["in", ["number", ["get", "i"]], ["array", ["literal", [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14]]]]
            """));

        Assert.NotNull(expression);

        var context = ExpressionTestsHelper.CreateContext(("i", 14d));

        Assert.Equal(true, expression!.Evaluate(context));
    }

    /// <summary>
    /// Verifies that the in expression supports substring checks on strings.
    /// </summary>
    [Fact]
    public void SubstringTest()
    {
        var expression = ExpressionParser.ParseExpression(JsonNode.Parse(
            """
            ["in", "ohn", "John Smith"]
            """));

        Assert.NotNull(expression);

        Assert.Equal(true, expression!.Evaluate(ExpressionTestsHelper.CreateContext()));
    }
}
