using System.Text.Json;
using System.Text.Json.Nodes;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles.Expressions;
using VecTiles.Styles.OpenMapTiles.Json.Converter;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace VecTiles.Styles.MapboxTests
{
    // Expression tests for "get", "match", "case" and "literal" with the icon
    // image evaluation of osm-liberty style variants in mind
    /// <summary>
    /// Expression tests for "get", "match", "case" and "literal" with the icon image evaluation of osm-liberty style variants in mind.
    /// </summary>
    public class OMTExpressionTests
    {
        const string IconImageExpression =
            """
            [
              "match",
              ["get", "subclass"],
              ["florist", "furniture", "soccer", "tennis"],
              ["get", "subclass"],
              ["get", "class"]
            ]
            """;

        /// <summary>
        /// Provides the input rows for the icon image match expression theory.
        /// </summary>
        public static TheoryData<string, object?, string?> MatchCases => new()
        {
            // Subclass equals one of the labels -> the subclass itself
            { "florist", null, "florist" },
            { "furniture", null, "furniture" },
            { "soccer", null, "soccer" },
            { "tennis", null, "tennis" },
            // Subclass differs from the labels -> the class
            { "parking", "parking", "parking" },
            { "school", "school", "school" },
            // Missing subclass -> the class
            { null, "hospital", "hospital" },
            // Neither subclass nor class -> null
            { null, null, null }
        };

        /// <summary>
        /// Verifies that the icon image match expression resolves the feature subclass to the expected output.
        /// </summary>
        /// <param name="subclass">The feature subclass value, or null if missing.</param>
        /// <param name="styleClass">The feature class value, or null if missing.</param>
        /// <param name="expected">The expected expression result.</param>
        [Theory]
        [MemberData(nameof(MatchCases))]
        public void IconImageMatchExpressionTest(string? subclass, object? styleClass, string? expected)
        {
            var expression = ExpressionParser.ParseExpression(JsonNode.Parse(IconImageExpression));

            Assert.NotNull(expression);

            var attributes = new Dictionary<string, object?>();

            if (subclass != null)
                attributes["subclass"] = subclass;

            if (styleClass != null)
                attributes["class"] = styleClass;

            var feature = new Feature(new Point(0, 0), new AttributesTable(attributes));
            var context = new EvaluationContext(14).WithFeature(feature);

            Assert.Equal(expected, expression.Evaluate(context));
        }

        /// <summary>
        /// Verifies that the match expression supports numeric labels.
        /// </summary>
        [Fact]
        public void MatchWithNumberLabelsTest()
        {
            var expression = ExpressionParser.ParseExpression(JsonNode.Parse(
                """
                ["match", ["get", "rank"], [1, 4], "low", [2, 3], "high", "none"]
                """));

            Assert.NotNull(expression);

            var context = new EvaluationContext(14).WithFeature(new Feature(new Point(0, 0), new AttributesTable(new Dictionary<string, object?> { ["rank"] = 2 })));

            Assert.Equal("high", expression.Evaluate(context));

            var otherContext = new EvaluationContext(14).WithFeature(new Feature(new Point(0, 0), new AttributesTable(new Dictionary<string, object?> { ["rank"] = 7 })));

            Assert.Equal("none", expression.Evaluate(otherContext));
        }

        /// <summary>
        /// Verifies that the case expression evaluates its conditions and falls back on a missing attribute.
        /// </summary>
        [Fact]
        public void CaseExpressionTest()
        {
            var expression = ExpressionParser.ParseExpression(JsonNode.Parse(
                """
                ["case", ["==", ["get", "brunnel"], "tunnel"], true, false]
                """));

            // "==" is a registered compound operator now
            Assert.NotNull(expression);

            var brunnelTunnelContext = new EvaluationContext(14).WithFeature(new Feature(new Point(0, 0), new AttributesTable(new Dictionary<string, object?> { ["brunnel"] = "tunnel" })));
            var brunnelRoadContext = new EvaluationContext(14).WithFeature(new Feature(new Point(0, 0), new AttributesTable(new Dictionary<string, object?> { ["brunnel"] = "road" })));

            Assert.Equal(true, expression!.Evaluate(brunnelTunnelContext));
            Assert.Equal(false, expression!.Evaluate(brunnelRoadContext));

            expression = ExpressionParser.ParseExpression(JsonNode.Parse(
                """
                ["case", ["get", "is_tunnel"], "tunnel_11", "road_11"]
                """));

            Assert.NotNull(expression);

            var tunnelContext = new EvaluationContext(14).WithFeature(new Feature(new Point(0, 0), new AttributesTable(new Dictionary<string, object?> { ["is_tunnel"] = true })));
            var roadContext = new EvaluationContext(14).WithFeature(new Feature(new Point(0, 0), new AttributesTable(new Dictionary<string, object?> { ["is_tunnel"] = false })));
            var missingContext = new EvaluationContext(14).WithFeature(new Feature(new Point(0, 0), new AttributesTable()));

            Assert.Equal("tunnel_11", expression.Evaluate(tunnelContext));
            Assert.Equal("road_11", expression.Evaluate(roadContext));
            Assert.Equal("road_11", expression.Evaluate(missingContext));
        }

        /// <summary>
        /// Verifies that the literal expression evaluates to its literal value.
        /// </summary>
        [Fact]
        public void LiteralExpressionTest()
        {
            var expression = ExpressionParser.ParseExpression(JsonNode.Parse(
                """
                ["literal", "traffic_sign"]
                """));

            Assert.NotNull(expression);

            var context = new EvaluationContext(14).WithFeature(new Feature(new Point(0, 0), new AttributesTable()));

            Assert.Equal("traffic_sign", expression.Evaluate(context));
        }

        /// <summary>
        /// Verifies that the stopped string converter parses expression based style values as an expression.
        /// </summary>
        [Fact]
        public void StoppedStringConverterParsesExpressionTest()
        {
            var converter = new StoppedStringConverter();
            var json = $"{{ \"icon-image\": {IconImageExpression} }}";
            var iconImage = JsonNode.Parse(json)!["icon-image"]!.ToJsonString();

            var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(iconImage));

            var stoppedString = converter.Read(ref reader, typeof(StoppedString), new JsonSerializerOptions());

            Assert.NotNull(stoppedString);
            Assert.NotNull(stoppedString!.Expression);
            Assert.False(stoppedString.HasOnlyDefault);
            Assert.False(stoppedString.IsConstant);

            var context = new EvaluationContext(14).WithFeature(new Feature(new Point(0, 0), new AttributesTable(new Dictionary<string, object?> { ["subclass"] = "furniture", ["class"] = "shop" })));

            Assert.Equal("furniture", stoppedString.Evaluate(context));
        }
    }
}
