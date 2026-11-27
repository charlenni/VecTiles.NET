using System.Text.Json;
using System.Text.Json.Nodes;
using VecTiles.Styles.MapLibre;
using VecTiles.Styles.MapLibre.Expressions;
using VecTiles.Styles.MapLibre.Json;
using VecTiles.Tests;

namespace VecTiles.Styles.MapboxTests;

/// <summary>
/// Tests of the "format" expression and the parsing of the shortbread based
/// Protomaps style (Files/PMTiles/style.json), which uses "format" with section
/// options objects and "is-supported-script" in its text fields.
/// </summary>
public class ExpressionFormatTests
{
    private static string StylePath => Path.Combine(RepoData.ProtomapsFiles, "protomaps-light-style.json");

    [Fact]
    public void FormatConcatenatesInputs()
    {
        var expression = ExpressionParser.ParseExpression(
            JsonNode.Parse("""["format", ["get", "name"], {}, " - ", {}, ["get", "code"], {}]"""));

        Assert.NotNull(expression);
        Assert.Equal("Zurich - ZH", expression!.Evaluate(ExpressionTestsHelper.CreateContext(
            [("name", "Zurich"), ("code", "ZH")])));
    }

    [Fact]
    public void FormatAcceptsOptionsWithExpressionsAndSkipsNullInputs()
    {
        var expression = ExpressionParser.ParseExpression(
            JsonNode.Parse("""
                ["format", ["coalesce", ["get", "name:en"], ["get", "name"]], {},
                 "\n", {}, ["coalesce", ["get", "pgf:name"], ["get", "name"]],
                 {"text-font": ["case", ["==", ["get", "script"], "Devanagari"],
                  ["literal", ["Noto Sans Devanagari Regular v1"]], ["literal", ["Noto Sans Regular"]]]}]
                """));

        Assert.NotNull(expression);
        Assert.Equal("Z\u00fcrich\nZ\u00fcrich", expression!.Evaluate(ExpressionTestsHelper.CreateContext(
            [("name", "Z\u00fcrich")])));
    }

    [Fact]
    public void FormatRejectsUnknownOption()
    {
        var expression = ExpressionParser.ParseExpression(
            JsonNode.Parse("""["format", "text", {"unknown-option": 1}]"""));

        Assert.Null(expression);
    }

    [Fact]
    public void FormatRejectsMissingInput()
    {
        var expression = ExpressionParser.ParseExpression(JsonNode.Parse("""["format"]"""));

        Assert.Null(expression);
    }

    [Fact]
    public void IsSupportedScriptReturnsTrue()
    {
        var expression = ExpressionParser.ParseExpression(
            JsonNode.Parse("""["is-supported-script", ["get", "name"]]"""));

        Assert.NotNull(expression);
        Assert.Equal(true, expression!.Evaluate(ExpressionTestsHelper.CreateContext(
            [("name", "\u05de\u05d1\u05d7\u05df")])));
    }

    [Fact]
    public void ShortbreadProtomapsStyleParses()
    {
        var json = File.ReadAllText(StylePath);

        var styleFile = JsonSerializer.Deserialize<StyleFile>(json, StyleJson.Options);

        Assert.NotNull(styleFile);
        Assert.NotEmpty(styleFile!.Layers);

        // The text fields of the style use "format" and "is-supported-script";
        // every layer must have parsed them (a parse failure throws while
        // deserializing, the following checks make sure the expressions exist)
        var labelLayers = styleFile.Layers
            .Where(layer => layer.Layout.TextField is { Expression: not null })
            .ToList();

        Assert.Contains(labelLayers, layer => layer.Layout.TextField!.Expression is FormatExpression);
    }
}
