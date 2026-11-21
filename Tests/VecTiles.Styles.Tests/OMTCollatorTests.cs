using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests based on the MapLibre Native expression equality fixtures collator1..collator5:
/// ["collator", { "case-sensitive": true, "diacritic-sensitive": true, "locale": "de" }] (also collator1..collator5)
/// every *.b variant differs in one collator option.
/// </summary>
public class OMTExpressionCollatorTests
{
    /// <summary>
    /// Provides the collator fixture base names used by the collator theories.
    /// </summary>
    public static IEnumerable<object?[]> CollatorFixtures =>
        new[]
        {
            new object?[] { "collator1" }, new object?[] { "collator2" }, new object?[] { "collator3" },
            new object?[] { "collator4" }, new object?[] { "collator5" }
        };

    /// <summary>
    /// Verifies that fixture a is structurally equal to a second parse of the same fixture.
    /// </summary>
    [Theory]
    [MemberData(nameof(CollatorFixtures))]
    public void FixtureIsStructurallyEqualToItselfTest(string fixture)
    {
        var expressionA1 = OMTExpressionFixture.Parse(fixture, "a");
        var expressionA2 = OMTExpressionFixture.Parse(fixture, "a");

        Assert.NotNull(expressionA1);
        Assert.NotNull(expressionA2);
        Assert.Equal(expressionA1, expressionA2);
    }

    /// <summary>
    /// Verifies that fixture a is not structurally equal to fixture b, which differs in exactly one leaf.
    /// </summary>
    [Theory]
    [MemberData(nameof(CollatorFixtures))]
    public void FixtureIsUnequalToOtherFixtureTest(string fixture)
    {
        var expressionA = OMTExpressionFixture.Parse(fixture, "a");
        var expressionB = OMTExpressionFixture.Parse(fixture, "b");

        Assert.NotNull(expressionA);
        Assert.NotNull(expressionB);
        Assert.NotEqual(expressionA, expressionB);
    }

    /// <summary>
    /// Verifies that the collator options from the collator1 fixtures control case sensitivity.
    /// </summary>
    [Fact]
    public void CaseSensitivityTest()
    {
        var caseSensitive = Assert.IsType<Collator>(OMTExpressionFixture.Parse("collator1", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        var caseInsensitive = Assert.IsType<Collator>(OMTExpressionFixture.Parse("collator1", "b")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));

        Assert.NotEqual(0, caseSensitive.Compare("a", "A"));
        Assert.Equal(0, caseInsensitive.Compare("a", "A"));
    }

    /// <summary>
    /// Verifies that the collator options from the collator2 fixtures control diacritic sensitivity.
    /// </summary>
    [Fact]
    public void DiacriticSensitivityTest()
    {
        var diacriticSensitive = Assert.IsType<Collator>(OMTExpressionFixture.Parse("collator2", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        var diacriticInsensitive = Assert.IsType<Collator>(OMTExpressionFixture.Parse("collator2", "b")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));

        Assert.NotEqual(0, diacriticSensitive.Compare("a", "\u00e4"));
        Assert.Equal(0, diacriticInsensitive.Compare("a", "\u00e4"));
    }

    /// <summary>
    /// Verifies that the collators use the locale configured by the collator3 fixtures.
    /// </summary>
    [Fact]
    public void LocaleTest()
    {
        var german = Assert.IsType<Collator>(OMTExpressionFixture.Parse("collator3", "a")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));
        var english = Assert.IsType<Collator>(OMTExpressionFixture.Parse("collator3", "b")!.Evaluate(OMTExpressionTestsHelper.CreateContext()));

        Assert.Equal("de", german.Locale);
        Assert.Equal("en", english.Locale);
    }
}
