using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Loads the expression fixtures from MapLibre Native
/// (https://github.com/maplibre/maplibre-native/tree/main/test/fixtures/expression_equality).
/// The fixtures come in pairs: *.a.json and *.b.json are two expressions that differ
/// in exactly one leaf, so the parsed expressions must be unequal.
/// </summary>
public static class OMTExpressionFixture
{
    /// <summary>
    /// The fixture directory is anchored at the base directory of the test assembly instead
    /// of the current working directory: tests of this assembly run in parallel and change
    /// the working directory (MFSpriteFactoryTests), which would break relative resolution.
    /// </summary>
    private static readonly string ExpressionDirectory =
        Path.Combine(AppContext.BaseDirectory, "Files", "Expressions");

    /// <summary>
    /// Parses a MapLibre Native expression equality fixture from the test fixture files.
    /// </summary>
    /// <param name="baseName">The fixture base name, for example "all".</param>
    /// <param name="variant">The fixture variant, "a" or "b".</param>
    /// <returns>The parsed expression, or null if the fixture cannot be parsed.</returns>
    public static IExpression? Parse(string baseName, string variant = "a")
    {
        var json = File.ReadAllText(Path.Combine(ExpressionDirectory, baseName + "." + variant + ".json"));

        return ExpressionParser.ParseExpression(JsonNode.Parse(json));
    }
}
