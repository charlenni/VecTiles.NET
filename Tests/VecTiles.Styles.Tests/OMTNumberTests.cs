using Newtonsoft.Json.Linq;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// number.a:
/// ["number", ["get", "x"]]
/// number.b: differs in the attribute key.
/// </summary>
public class OMTNumberTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("number", "a");
        var expressionA2 = OMTExpressionFixture.Parse("number", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("number", "a");
        var expressionB = OMTExpressionFixture.Parse("number", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expression = OMTExpressionFixture.Parse("number", "a");

        Assert.NotNull(expression);

        Assert.Equal(42.5d, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", 42.5d))));
    }

    [Fact]
    public void FallbackTest()
    {
        var expression = ExpressionParser.ParseExpression(JToken.Parse("[\"number\", [\"get\", \"x\"], [\"number\", [\"get\", \"y\"]], 0]"));

        Assert.NotNull(expression);

        Assert.Equal(3d, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("y", 3d))));
        Assert.Equal(0d, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
