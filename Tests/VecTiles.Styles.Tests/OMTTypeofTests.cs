using Newtonsoft.Json.Linq;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// typeof.a:
/// ["typeof", ["get", "x"]]
/// typeof.b: differs in the attribute key.
/// </summary>
public class OMTTypeofTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("typeof", "a");
        var expressionA2 = OMTExpressionFixture.Parse("typeof", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("typeof", "a");
        var expressionB = OMTExpressionFixture.Parse("typeof", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Theory]
    [InlineData(1d, "number")]
    [InlineData("text", "string")]
    [InlineData(true, "boolean")]
    [InlineData(null, "null")]
    public void FixtureEvaluationTest(object? x, string expected)
    {
        var expression = OMTExpressionFixture.Parse("typeof", "a");

        Assert.NotNull(expression);

        Assert.Equal(expected, expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", x))));
    }

    [Fact]
    public void ArrayTypeOfTest()
    {
        var expression = OMTExpressionFixture.Parse("typeof", "a");

        Assert.NotNull(expression);

        Assert.Equal("array", expression!.Evaluate(OMTExpressionTestsHelper.CreateContext(("x", new object?[] { 1d, 2d }))));
    }
}
