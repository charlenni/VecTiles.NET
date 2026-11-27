using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// number-format-default.a:
/// ["number-format", 123456.789, {}]
/// number-format-default.b: differs in the sign of the number.
/// </summary>
public class ExpressionNumberFormatDefaultTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("number-format-default", "a");
        var expressionA2 = ExpressionFixture.Parse("number-format-default", "a");

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
        var expressionA = ExpressionFixture.Parse("number-format-default", "a");
        var expressionB = ExpressionFixture.Parse("number-format-default", "b");

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
        var expressionA = ExpressionFixture.Parse("number-format-default", "a");
        var expressionB = ExpressionFixture.Parse("number-format-default", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);

        Assert.Equal("123456.789", expressionA!.Evaluate(ExpressionTestsHelper.CreateContext()));
        Assert.Equal("-123456.789", expressionB!.Evaluate(ExpressionTestsHelper.CreateContext()));
    }
}
