using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// number-format-precision.a:
/// ["number-format", 987654321.23456789, { "locale": "en-US", "min-fraction-digits": 15, "max-fraction-digits": 20 }]
/// number-format-precision.b: differs in the number of fraction digits.
/// </summary>
public class ExpressionNumberFormatPrecisionTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("number-format-precision", "a");
        var expressionA2 = ExpressionFixture.Parse("number-format-precision", "a");

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
        var expressionA = ExpressionFixture.Parse("number-format-precision", "a");
        var expressionB = ExpressionFixture.Parse("number-format-precision", "b");

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
        var expression = ExpressionFixture.Parse("number-format-precision", "a");

        Assert.NotNull(expression);

        var result = expression!.Evaluate(ExpressionTestsHelper.CreateContext());

        Assert.IsType<string>(result);

        var digits = ((string)result!).Split('.')[1].Length;

        // The maximum number of fraction digits is used for the formatting
        Assert.Equal(20, digits);
        Assert.StartsWith("987,654,321.2345", (string)result!);
    }
}
