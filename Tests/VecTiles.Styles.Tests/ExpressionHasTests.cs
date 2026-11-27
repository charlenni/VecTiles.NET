using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// has.a:
/// ["has", "x"]
/// has.b: differs in the attribute key.
/// </summary>
public class ExpressionHasTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("has", "a");
        var expressionA2 = ExpressionFixture.Parse("has", "a");

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
        var expressionA = ExpressionFixture.Parse("has", "a");
        var expressionB = ExpressionFixture.Parse("has", "b");

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
        var expression = ExpressionFixture.Parse("has", "a");

        Assert.NotNull(expression);

        Assert.Equal(true, expression!.Evaluate(ExpressionTestsHelper.CreateContext(("x", true))));
    }

    /// <summary>
    /// Verifies that the has expression reports whether the attribute key exists, even for null values.
    /// </summary>
    /// <param name="key">The attribute key to set.</param>
    /// <param name="value">The attribute value to set.</param>
    [Theory]
    [InlineData("x", true)]
    [InlineData("x", null)]
    [InlineData("y_other", true)]
    public void AttributeExistenceTest(string key, object? value)
    {
        var expression = ExpressionFixture.Parse("has", "a");

        Assert.NotNull(expression);

        Assert.Equal(key == "x", expression!.Evaluate(ExpressionTestsHelper.CreateContext((key, value))));
    }
}
