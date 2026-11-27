using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// number-format-currency.a:
/// ["number-format", 123456.789, { "locale": "de-DE", "currency": "EUR" }]
/// number-format-currency.b: differs in the locale and currency.
/// </summary>
public class ExpressionNumberFormatCurrencyTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("number-format-currency", "a");
        var expressionA2 = ExpressionFixture.Parse("number-format-currency", "a");

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
        var expressionA = ExpressionFixture.Parse("number-format-currency", "a");
        var expressionB = ExpressionFixture.Parse("number-format-currency", "b");

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
        var expression = ExpressionFixture.Parse("number-format-currency", "a");

        Assert.NotNull(expression);

        Assert.Equal("123.456,79 \u20ac", expression!.Evaluate(ExpressionTestsHelper.CreateContext()));
    }
}
