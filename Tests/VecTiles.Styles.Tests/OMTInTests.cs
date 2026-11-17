using Newtonsoft.Json.Linq;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// in.a:
/// ["number", ["in", ["number", ["get", "i"]], ["array", ["get", "arr"]]]]
/// in.b: differs in the array attribute key.
/// </summary>
public class OMTInTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("in", "a");
        var expressionA2 = OMTExpressionFixture.Parse("in", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("in", "a");
        var expressionB = OMTExpressionFixture.Parse("in", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Theory]
    [InlineData(20d, true)]
    [InlineData(1d, false)]
    public void MembershipTest(double i, bool expected)
    {
        // Membership without the wrapping number assertion of the fixture
        var expression = ExpressionParser.ParseExpression(JToken.Parse(
            """
            ["in", ["number", ["get", "i"]], ["array", ["get", "arr"]]]
            """));

        Assert.NotNull(expression);

        var context = OMTExpressionTestsHelper.CreateContext(("i", i), ("arr", new object?[] { 10d, 20d, 30d }));

        Assert.Equal(expected, expression!.Evaluate(context));
    }

    [Fact]
    public void LargeHaystackUsesHashSetTest()
    {
        var expression = ExpressionParser.ParseExpression(JToken.Parse(
            """
            ["in", ["number", ["get", "i"]], ["array", ["literal", [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14]]]]
            """));

        Assert.NotNull(expression);

        var context = OMTExpressionTestsHelper.CreateContext(("i", 14d));

        Assert.Equal(true, expression!.Evaluate(context));
    }

    [Fact]
    public void SubstringTest()
    {
        var expression = ExpressionParser.ParseExpression(JToken.Parse(
            """
            ["in", "ohn", "John Smith"]
            """));

        Assert.NotNull(expression);

        Assert.Equal(true, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
