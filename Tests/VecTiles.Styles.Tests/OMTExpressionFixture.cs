using Newtonsoft.Json.Linq;
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
    public static IExpression? Parse(string baseName, string variant = "a")
    {
        var json = File.ReadAllText(Path.Combine("Files", "Expressions", baseName + "." + variant + ".json"));

        return ExpressionParser.ParseExpression(JToken.Parse(json));
    }
}
