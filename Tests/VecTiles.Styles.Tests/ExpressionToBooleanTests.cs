using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures
/// to-boolean.a:
/// ["to-boolean", ["get", "x"]]
/// to-boolean.b: differs in the attribute key.
/// </summary>
public class ExpressionToBooleanTests
{
    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Fact]
    public void FixtureIsStructurallyEqualToItselfTest()
    {
        var expressionA1 = ExpressionFixture.Parse("to-boolean", "a");
        var expressionA2 = ExpressionFixture.Parse("to-boolean", "a");

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
        var expressionA = ExpressionFixture.Parse("to-boolean", "a");
        var expressionB = ExpressionFixture.Parse("to-boolean", "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    /// <summary>
    /// Verifies that the fixtures evaluate to the expected results.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(0d, false)]
    [InlineData(1d, true)]
    [InlineData("yes", true)]
    [InlineData("", false)]
    public void FixtureEvaluationTest(object? x, bool expected)
    {
        var expression = ExpressionFixture.Parse("to-boolean", "a");

        Assert.NotNull(expression);

        Assert.Equal(expected, expression!.Evaluate(ExpressionTestsHelper.CreateContext(("x", x))));
    }

    /// <summary>
    /// Verifies that a missing attribute is coerced to false, unlike the boolean assertion.
    /// </summary>
    [Fact]
    public void MissingAttributeTest()
    {
        var expression = ExpressionFixture.Parse("to-boolean", "a");

        Assert.NotNull(expression);

        // A missing attribute is false, unlike the assertions
        Assert.Equal(false, expression!.Evaluate(ExpressionTestsHelper.CreateContext()));
    }
}
