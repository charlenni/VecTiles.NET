using Newtonsoft.Json.Linq;
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
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("boolean", "a");
        var expressionA2 = OMTExpressionFixture.Parse("boolean", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("boolean", "a");
        var expressionB = OMTExpressionFixture.Parse("boolean", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expression = OMTExpressionFixture.Parse("boolean", "a");

        Assert.NotNull(expression);

        Assert.Equal(true, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", true))));
    }

    [Fact]
    public void NonBooleanWithoutFallbackTest()
    {
        var expression = OMTExpressionFixture.Parse("boolean", "a");

        Assert.NotNull(expression);

        // Numbers are not booleans and there is no fallback
        Assert.Null(expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", 1d))));
    }

    [Fact]
    public void MissingAttributeWithoutFallbackTest()
    {
        var expression = OMTExpressionFixture.Parse("boolean", "a");

        Assert.NotNull(expression);

        Assert.Null(expression!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
