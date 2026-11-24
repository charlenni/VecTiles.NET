using NetTopologySuite.Geometries;
using NetTopologySuite.Features;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapsforge.Enums;
using VecTiles.Styles.Mapsforge.Expressions;

namespace VecTiles.Styles.Mapsforge.Tests
{
    /// <summary>
    /// Tests for the Mapsforge rule condition expressions: element type,
    /// closed way and tag matches, and their combination.
    /// </summary>
    public class MFExpressionTests
    {
        private static EvaluationContext Context(AttributesTable? attributes = null, Geometry? geometry = null)
        {
            var feature = geometry == null ? null : new Feature(geometry, attributes ?? new AttributesTable());

            return new EvaluationContext(5, attributes: attributes, feature: feature);
        }

        // --- MFElementExpression ---

        [Fact]
        public void ElementNodeMatchesPointGeometryTest()
        {
            var expression = new MFElementExpression(MapsforgeElement.Node);

            Assert.Equal(true, expression.Evaluate(Context(geometry: new Point(1, 2))));
        }

        [Fact]
        public void ElementNodeDoesNotMatchLineGeometryTest()
        {
            var expression = new MFElementExpression(MapsforgeElement.Node);
            var line = new LineString([new Coordinate(0, 0), new Coordinate(1, 1)]);

            Assert.Equal(false, expression.Evaluate(Context(geometry: line)));
        }

        [Fact]
        public void ElementWayMatchesLineAndPolygonGeometryTest()
        {
            var expression = new MFElementExpression(MapsforgeElement.Way);
            var line = new LineString([new Coordinate(0, 0), new Coordinate(1, 1)]);
            var polygon = new Polygon(new LinearRing([new Coordinate(0, 0), new Coordinate(1, 0),
                new Coordinate(1, 1), new Coordinate(0, 0)]));

            Assert.Equal(true, expression.Evaluate(Context(geometry: line)));
            Assert.Equal(true, expression.Evaluate(Context(geometry: polygon)));
        }

        [Fact]
        public void ElementAnyMatchesEverythingTest()
        {
            var expression = new MFElementExpression(MapsforgeElement.Any);

            Assert.Equal(true, expression.Evaluate(Context(geometry: new Point(0, 0))));
            Assert.Equal(true, expression.Evaluate(Context(geometry: null)));
        }

        [Fact]
        public void ElementWithoutFeatureDoesNotMatchTest()
        {
            var expression = new MFElementExpression(MapsforgeElement.Node);

            Assert.Equal(false, expression.Evaluate(new EvaluationContext(5)));
        }

        // --- MFClosedExpression ---

        [Fact]
        public void ClosedYesMatchesPolygonsOnlyTest()
        {
            var expression = new MFClosedExpression(MapsforgeClosed.Yes);
            var line = new LineString([new Coordinate(0, 0), new Coordinate(1, 1)]);
            var polygon = new Polygon(new LinearRing([new Coordinate(0, 0), new Coordinate(1, 0),
                new Coordinate(1, 1), new Coordinate(0, 0)]));

            Assert.Equal(true, expression.Evaluate(Context(geometry: polygon)));
            Assert.Equal(false, expression.Evaluate(Context(geometry: line)));
        }

        [Fact]
        public void ClosedNoMatchesLinesOnlyTest()
        {
            var expression = new MFClosedExpression(MapsforgeClosed.No);
            var line = new LineString([new Coordinate(0, 0), new Coordinate(1, 1)]);
            var polygon = new Polygon(new LinearRing([new Coordinate(0, 0), new Coordinate(1, 0),
                new Coordinate(1, 1), new Coordinate(0, 0)]));

            Assert.Equal(true, expression.Evaluate(Context(geometry: line)));
            Assert.Equal(false, expression.Evaluate(Context(geometry: polygon)));
        }

        [Fact]
        public void ClosedAnyMatchesEverythingTest()
        {
            var expression = new MFClosedExpression(MapsforgeClosed.Any);

            Assert.Equal(true, expression.Evaluate(Context(geometry: null)));
        }

        // --- MFTagMatchExpression ---

        [Fact]
        public void TagMatchPositiveValueTest()
        {
            var expression = new MFTagMatchExpression(["highway"], ["primary", "secondary"], MapsforgeMatchMode.Positive);

            Assert.Equal(true, expression.Evaluate(Context(new AttributesTable { { "highway", "primary" } })));
            Assert.Equal(false, expression.Evaluate(Context(new AttributesTable { { "highway", "footway" } })));
            Assert.Equal(false, expression.Evaluate(Context(new AttributesTable { { "amenity", "primary" } })));
        }

        [Fact]
        public void TagMatchMultipleKeysTest()
        {
            var expression = new MFTagMatchExpression(["highway", "railway"], ["*"], MapsforgeMatchMode.Positive);

            Assert.Equal(true, expression.Evaluate(Context(new AttributesTable { { "railway", "rail" } })));
            Assert.Equal(false, expression.Evaluate(Context(new AttributesTable { { "waterway", "river" } })));
        }

        [Fact]
        public void TagMatchAnyKeyAnyValueTest()
        {
            var expression = new MFTagMatchExpression([], [], MapsforgeMatchMode.Positive);

            Assert.Equal(true, expression.Evaluate(Context(new AttributesTable { { "anything", "goes" } })));
        }

        [Fact]
        public void TagMatchWithoutAttributesOnlyWildcardMatchesTest()
        {
            var wildcard = new MFTagMatchExpression([], [], MapsforgeMatchMode.Positive);
            var tagRule = new MFTagMatchExpression(["highway"], ["*"], MapsforgeMatchMode.Positive);

            Assert.Equal(true, wildcard.Evaluate(Context()));
            Assert.Equal(false, tagRule.Evaluate(Context()));
        }

        [Fact]
        public void TagMatchNegationMatchesMissingAndListedValuesTest()
        {
            // "~|primary": negation, matches when highway is absent and when it is "primary"
            var expression = new MFTagMatchExpression(["highway"], ["~", "primary"], MapsforgeMatchMode.Negation);

            Assert.Equal(true, expression.Evaluate(Context(new AttributesTable { { "amenity", "bench" } })));
            Assert.Equal(true, expression.Evaluate(Context(new AttributesTable { { "highway", "primary" } })));
            Assert.Equal(false, expression.Evaluate(Context(new AttributesTable { { "highway", "footway" } })));
        }

        [Fact]
        public void TagMatchExclusionMatchesMissingAndUnlistedValuesTest()
        {
            // "-|footway": exclusion, matches when highway is absent and when it is not "footway"
            var expression = new MFTagMatchExpression(["highway"], ["-", "footway"], MapsforgeMatchMode.Exclusion);

            Assert.Equal(true, expression.Evaluate(Context(new AttributesTable { { "amenity", "bench" } })));
            Assert.Equal(true, expression.Evaluate(Context(new AttributesTable { { "highway", "primary" } })));
            Assert.Equal(false, expression.Evaluate(Context(new AttributesTable { { "highway", "footway" } })));
        }

        [Fact]
        public void TagMatchNegationWildcardMeansKeyNotPresentTest()
        {
            // "religion=~|*": the Mapsforge rule engine (RuleBuilder, NegativeMatcher) removes
            // the "~" marker and matches the remaining values literally, so the "*" is NOT a
            // wildcard and the rule means "religion not present" (the place-of-worship icons:
            // unknown.svg was also drawn when religion was present, covering christian.svg)
            var expression = new MFTagMatchExpression(["religion"], ["~", "*"], MapsforgeMatchMode.Negation);

            Assert.Equal(true, expression.Evaluate(Context(new AttributesTable { { "amenity", "place_of_worship" } })));
            Assert.Equal(false, expression.Evaluate(Context(new AttributesTable
            {
                { "amenity", "place_of_worship" },
                { "religion", "christian" },
            })));
        }

        [Fact]
        public void TagMatchNegationListedValuesTest()
        {
            // "tunnel=~|no|false": matches when tunnel is absent, "no" or "false"
            var expression = new MFTagMatchExpression(["tunnel"], ["~", "no", "false"], MapsforgeMatchMode.Negation);

            Assert.Equal(true, expression.Evaluate(Context(new AttributesTable { { "highway", "primary" } })));
            Assert.Equal(true, expression.Evaluate(Context(new AttributesTable { { "tunnel", "no" } })));
            Assert.Equal(false, expression.Evaluate(Context(new AttributesTable { { "tunnel", "yes" } })));
        }

        // --- MFAllExpression ---

        [Fact]
        public void AllExpressionEmptyIsAlwaysTrueTest()
        {
            Assert.True(MFAllExpression.Empty.IsEmpty);
            Assert.Equal(true, MFAllExpression.Empty.Evaluate(Context()));
        }

        [Fact]
        public void AllExpressionRequiresAllConditionsTest()
        {
            var tagMatch = new MFTagMatchExpression(["highway"], ["primary"], MapsforgeMatchMode.Positive);
            var closed = new MFClosedExpression(MapsforgeClosed.No);
            var conjunction = new MFAllExpression([tagMatch, closed]);

            var line = new LineString([new Coordinate(0, 0), new Coordinate(1, 1)]);
            var polygon = new Polygon(new LinearRing([new Coordinate(0, 0), new Coordinate(1, 0),
                new Coordinate(1, 1), new Coordinate(0, 0)]));

            Assert.Equal(true, conjunction.Evaluate(Context(new AttributesTable { { "highway", "primary" } }, line)));
            Assert.Equal(false, conjunction.Evaluate(Context(new AttributesTable { { "highway", "primary" } }, polygon)));
            Assert.Equal(false, conjunction.Evaluate(Context(new AttributesTable { { "highway", "footway" } }, line)));
        }

        [Fact]
        public void AllExpressionAndCombinesTest()
        {
            var first = new MFAllExpression([
                new MFTagMatchExpression(["highway"], ["primary"], MapsforgeMatchMode.Positive)]);
            var second = new MFAllExpression([
                new MFClosedExpression(MapsforgeClosed.No)]);

            var combined = first.And(second);

            var line = new LineString([new Coordinate(0, 0), new Coordinate(1, 1)]);

            Assert.Equal(true, combined.Evaluate(Context(new AttributesTable { { "highway", "primary" } }, line)));
            Assert.Equal(false, combined.Evaluate(Context(new AttributesTable { { "highway", "footway" } }, line)));
        }

        [Fact]
        public void AllExpressionPossibleOutputsTest()
        {
            var possibleOutputs = Assert.IsType<bool[]>(MFAllExpression.Empty.PossibleOutputs());

            Assert.Contains(true, possibleOutputs);
            Assert.Contains(false, possibleOutputs);
        }
    }
}
