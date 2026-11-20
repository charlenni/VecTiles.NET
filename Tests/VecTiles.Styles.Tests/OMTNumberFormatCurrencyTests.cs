using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// number-format-currency.a:
/// ["number-format", 123456.789, { "locale": "de-DE", "currency": "EUR" }]
/// number-format-currency.b: differs in the locale and currency.
/// </summary>
public class OMTNumberFormatCurrencyTests
{
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = OMTExpressionFixture.Parse("number-format-currency", "a");
        var expressionA2 = OMTExpressionFixture.Parse("number-format-currency", "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    [Fact]
    public void FixtureIsUnequalToOtherFixtureTest()
    {
        var expressionA = OMTExpressionFixture.Parse("number-format-currency", "a");
        var expressionB = OMTExpressionFixture.Parse("number-format-currency", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    [Fact]
    public void FixtureEvaluationTest()
    {
        var expression = OMTExpressionFixture.Parse("number-format-currency", "a");

        Assert.NotNull(expression);

        Assert.Equal("123.456,79 \u20ac", expression!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
    }
}
