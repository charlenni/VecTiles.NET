using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// divide.a:
/// ["/", 10, 5]
/// divide.b: differs in the divisor.
/// </summary>
public class OMTDivideTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("divide", "a");
        var expressionA2 = OMTExpressionFixture.Parse("divide", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("divide", "a");
        var expressionB = OMTExpressionFixture.Parse("divide", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expressionA = OMTExpressionFixture.Parse("divide", "a");
        var expressionB = OMTExpressionFixture.Parse("divide", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);

        Assert.Equal(2d, expressionA!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        Assert.Equal(10d / 6d, expressionB!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }

    [Fact]
    public void DivideByZeroTest()
    {
        var expression = ExpressionParser.ParseExpression(JsonNode.Parse("[\"/\", 10, 0]"));

        Assert.NotNull(expression);
        Assert.Null(expression!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
