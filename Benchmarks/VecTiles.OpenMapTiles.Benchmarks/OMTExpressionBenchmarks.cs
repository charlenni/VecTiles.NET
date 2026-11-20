using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using System.Text.Json.Nodes;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.OpenMapTiles.Benchmarks;

/// <summary>
/// Benchmark tests for the OpenMapTiles expression system. Expressions are
/// evaluated for every single feature like the filters, so the filter classes
/// they are going to replace are benchmarked alongside for comparison.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class OMTExpressionBenchmarks
{
    // Test data configurations (like OMTFilterBenchmarks)
    private const int SmallListSize = 10;
    private const int MediumListSize = 100;
    private const int LargeListSize = 1000;
    private const int FeatureCount = 1000;

    // Test features with a matching evaluation context per feature
    private IFeature[] _features;
    private EvaluationContext[] _contexts;
    private IFeature _featureWithAllAttributes;
    private IFeature _featureWithNumericAttributes;
    private IFeature _featureWithoutOptionalAttributes;
    private EvaluationContext _contextWithAllAttributes;
    private EvaluationContext _contextWithNumericAttributes;
    private EvaluationContext _contextWithoutOptionalAttributes;

    // Expressions
    private IExpression _getExpression;
    private IExpression _literalExpression;

    private IExpression _equalsString;
    private IExpression _equalsInt;
    private IExpression _notEqualsString;
    private IExpression _lessThanFloat;
    private IExpression _greaterThanFloat;

    private IExpression _inSmall;
    private IExpression _inMedium;
    private IExpression _inLarge;
    private IExpression _inSmallNumbers;

    private IExpression _hasFilterExisting;
    private IExpression _hasMissing;

    private IExpression _allExpression;
    private IExpression _anyExpression;
    private IExpression _notExpression;

    private IExpression _matchExpression;
    private IExpression _caseExpression;
    private IExpression _coalesceExpression;

    private IExpression _stepExpression;
    private IExpression _interpolateExpression;

    private IExpression _numberAssertion;
    private IExpression _toStringExpression;
    private IExpression _concatExpression;
    private IExpression _upcaseExpression;
    private IExpression _plusExpression;
    private IExpression _minExpression;
    private IExpression _sqrtExpression;

    private IExpression _collatorEquals;

    [GlobalSetup]
    public void Setup()
    {
        _features = CreateTestFeatures(FeatureCount);
        _contexts = new EvaluationContext[_features.Length];

        for (int i = 0; i < _features.Length; i++)
            _contexts[i] = new EvaluationContext(14).WithFeature(_features[i]);

        _featureWithAllAttributes = CreateFeatureWithAllAttributes();
        _featureWithNumericAttributes = CreateFeatureWithNumericAttributes();
        _featureWithoutOptionalAttributes = CreateFeatureWithoutOptionalAttributes();

        _contextWithAllAttributes = new EvaluationContext(14).WithFeature(_featureWithAllAttributes);
        _contextWithNumericAttributes = new EvaluationContext(14).WithFeature(_featureWithNumericAttributes);
        _contextWithoutOptionalAttributes = new EvaluationContext(14).WithFeature(_featureWithoutOptionalAttributes);

        // Attribute access and literals
        _getExpression = Parse("""["get", "name"]""");
        _literalExpression = Parse("""["literal", "TestFeature"]""");

        // Comparisons: the future replacement of the EqualityFilter family
        _equalsString = Parse("""["==", ["get", "name"], "TestFeature"]""");
        _equalsInt = Parse("""["==", ["get", "level"], 5]""");
        _notEqualsString = Parse("""["!=", ["get", "name"], "OtherFeature"]""");
        _lessThanFloat = Parse("""["<", ["get", "elevation"], 200.0]""");
        _greaterThanFloat = Parse("""[">", ["get", "elevation"], 100.0]""");

        // Membership: the future replacement of InFilter including its
        // HashSet optimization for large lists
        _inSmall = Parse(InArray(SmallListSize));
        _inMedium = Parse(InArray(MediumListSize));
        _inLarge = Parse(InArray(LargeListSize));
        _inSmallNumbers = Parse("""["in", ["get", "level"], ["literal", [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]]]""");

        _hasFilterExisting = Parse("""["has", "name"]""");
        _hasMissing = Parse("""["has", "nonexistent"]""");

        // Logic with short-circuit evaluation
        _allExpression = Parse(
            """
            ["all", ["==", ["get", "name"], "TestFeature"], ["has", "level"], [">", ["get", "elevation"], 50.0]]
            """);
        _anyExpression = Parse(
            """
            ["any", ["==", ["get", "name"], "TestFeature"], ["has", "level"], [">", ["get", "elevation"], 50.0]]
            """);
        _notExpression = Parse("""["!", ["boolean", ["get", "name"]]]""");

        // Dispatching expressions
        _matchExpression = Parse(
            """
            ["match", ["get", "name"], "TestFeature", "matching", "not-matching"]
            """);
        _caseExpression = Parse(
            """
            ["case", ["==", ["get", "name"], "TestFeature"], "matching", "not-matching"]
            """);
        _coalesceExpression = Parse(
            """
            ["coalesce", ["get", "description"], ["get", "name"], "default"]
            """);

        // Zoom expressions
        _stepExpression = Parse(
            """
            ["step", ["zoom"], 1.0, 10, 2.0, 14, 4.0]
            """);
        _interpolateExpression = Parse(
            """
            ["interpolate", ["exponential", 2], ["zoom"], 10, 1.0, 18, 8.0]
            """);

        // Type assertions, coercions and strings
        _numberAssertion = Parse("""["number", ["get", "level"]]""");
        _toStringExpression = Parse("""["to-string", ["get", "level"]]""");
        _concatExpression = Parse("""["concat", "Layer/", ["get", "name"]]""");
        _upcaseExpression = Parse("""["upcase", ["get", "name"]]""");

        // Arithmetic
        _plusExpression = Parse("""["+", ["get", "level"], ["get", "elevation"], 1]""");
        _minExpression = Parse("""["min", ["get", "elevation"], 150]""");
        _sqrtExpression = Parse("""["sqrt", ["get", "elevation"]]""");

        // Collator comparisons
        _collatorEquals = Parse(
            """
            ["==", ["get", "name"], "TestFeature", ["collator", { "case-sensitive": true }]]
            """);
    }

    private static IExpression Parse(string json)
    {
        return ExpressionParser.ParseExpression(JsonNode.Parse(json))!;
    }

    private static string InArray(int size) => $$"""
        ["in", ["get", "type"], ["literal", [{{string.Join(", ", Enumerable.Range(0, size).Select(i => $"\"name_{i}\""))}}]]]
        """;

    private IFeature[] CreateTestFeatures(int count)
    {
        var features = new IFeature[count];

        for (int i = 0; i < count; i++)
        {
            var attributes = new AttributesTable();
            attributes.Add("id", $"feature_{i}");
            attributes.Add("name", i % 2 == 0 ? "TestFeature" : "OtherFeature");
            attributes.Add("level", i % 10);
            attributes.Add("elevation", 100.0f + i % 200);
            attributes.Add("type", $"type_{i % 20}");

            Geometry geometry;
            int geometryType = i % 3;
            if (geometryType == 0)
            {
                geometry = new Point(0, 0);
            }
            else if (geometryType == 1)
            {
                geometry = new LineString(new[] { new Coordinate(0, 0), new Coordinate(1, 1) });
            }
            else
            {
                geometry = new Polygon(new LinearRing(new[] {
                    new Coordinate(0, 0), new Coordinate(1, 0), new Coordinate(1, 1), new Coordinate(0, 1), new Coordinate(0, 0)
                }));
            }

            features[i] = new Feature(geometry, attributes);
        }

        return features;
    }

    private IFeature CreateFeatureWithAllAttributes()
    {
        var attributes = new AttributesTable();
        attributes.Add("id", "feature_complete");
        attributes.Add("name", "TestFeature");
        attributes.Add("level", 5);
        attributes.Add("elevation", 150.5f);
        attributes.Add("type", "building");
        attributes.Add("description", "A test feature with all attributes");

        return new Feature(new Point(0, 0), attributes);
    }

    private IFeature CreateFeatureWithNumericAttributes()
    {
        var attributes = new AttributesTable();
        attributes.Add("id", "numeric_feature");
        attributes.Add("level", 10);
        attributes.Add("elevation", 250.0f);
        attributes.Add("population", 1000L);
        attributes.Add("temperature", -5.5);

        return new Feature(new Point(1, 1), attributes);
    }

    private IFeature CreateFeatureWithoutOptionalAttributes()
    {
        var attributes = new AttributesTable();
        attributes.Add("id", "minimal_feature");

        return new Feature(new LineString(new[] { new Coordinate(0, 0), new Coordinate(1, 1) }), attributes);
    }

    // =========================================================================
    // ATTRIBUTE ACCESS
    // =========================================================================

    [Benchmark(Baseline = true)]
    public bool Get()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= _getExpression.Evaluate(_contexts[i]) != null;

        return result;
    }

    [Benchmark]
    public bool Literal()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= _literalExpression.Evaluate(_contexts[i]) != null;

        return result;
    }

    // =========================================================================
    // COMPARISONS (with the equivalent filter next to it)
    // =========================================================================

    [Benchmark]
    public bool Equals_String()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_equalsString.Evaluate(_contexts[i])!;

        return result;
    }

    [Benchmark]
    public bool Equals_Int()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_equalsInt.Evaluate(_contexts[i])!;

        return result;
    }

    [Benchmark]
    public bool NotEquals_String()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_notEqualsString.Evaluate(_contexts[i])!;

        return result;
    }

    [Benchmark]
    public bool LessThan_Float()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_lessThanFloat.Evaluate(_contexts[i])!;

        return result;
    }

    [Benchmark]
    public bool GreaterThan_Float()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_greaterThanFloat.Evaluate(_contexts[i])!;

        return result;
    }

    // =========================================================================
    // MEMBERSHIP "in" (HashSet optimization of InFilter, with the filter)
    // =========================================================================

    [Benchmark]
    public bool In_SmallList()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_inSmall.Evaluate(_contexts[i])!;

        return result;
    }

    [Benchmark]
    public bool In_MediumList()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_inMedium.Evaluate(_contexts[i])!;

        return result;
    }

    [Benchmark]
    public bool In_LargeList()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_inLarge.Evaluate(_contexts[i])!;

        return result;
    }

    [Benchmark]
    public bool In_NumberList()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_inSmallNumbers.Evaluate(_contexts[i])!;

        return result;
    }

    [Benchmark]
    public bool Has_Existing()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_hasFilterExisting.Evaluate(_contexts[i])!;

        return result;
    }

    [Benchmark]
    public bool Has_Missing()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_hasMissing.Evaluate(_contexts[i])!;

        return result;
    }

    // =========================================================================
    // LOGIC (short-circuit like the AllFilter/AnyFilter family)
    // =========================================================================

    [Benchmark]
    public bool All()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_allExpression.Evaluate(_contexts[i])!;

        return result;
    }

    [Benchmark]
    public bool Any()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_anyExpression.Evaluate(_contexts[i])!;

        return result;
    }

    [Benchmark]
    public bool Not()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_notExpression.Evaluate(_contexts[i])!;

        return result;
    }

    // =========================================================================
    // DISPATCHING EXPRESSIONS
    // =========================================================================

    [Benchmark]
    public bool Match()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= ((string)_matchExpression.Evaluate(_contexts[i])!).Length > 0;

        return result;
    }

    [Benchmark]
    public bool Case()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= ((string)_caseExpression.Evaluate(_contexts[i])!).Length > 0;

        return result;
    }

    [Benchmark]
    public bool Coalesce()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= _coalesceExpression.Evaluate(_contexts[i]) != null;

        return result;
    }

    // =========================================================================
    // ZOOM EXPRESSIONS
    // =========================================================================

    [Benchmark]
    public bool Step()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (double)_stepExpression.Evaluate(_contexts[i])! > 0;

        return result;
    }

    [Benchmark]
    public bool Interpolate_Exponential()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (double)_interpolateExpression.Evaluate(_contexts[i])! > 0;

        return result;
    }

    // =========================================================================
    // COERCIONS AND STRING OPERATIONS
    // =========================================================================

    [Benchmark]
    public bool NumberAssertion()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= _numberAssertion.Evaluate(_contexts[i]) != null;

        return result;
    }

    [Benchmark]
    public bool ToString_()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (string)_toStringExpression.Evaluate(_contexts[i])! != null;

        return result;
    }

    [Benchmark]
    public bool Concat()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (_concatExpression.Evaluate(_contexts[i]) as string)!.Length > 0;

        return result;
    }

    [Benchmark]
    public bool Upcase()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (_upcaseExpression.Evaluate(_contexts[i]) as string)!.Length > 0;

        return result;
    }

    // =========================================================================
    // ARITHMETIC
    // =========================================================================

    [Benchmark]
    public bool Plus()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (double)_plusExpression.Evaluate(_contexts[i])! > 0;

        return result;
    }

    [Benchmark]
    public bool Min()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (double)_minExpression.Evaluate(_contexts[i])! >= 0;

        return result;
    }

    [Benchmark]
    public bool Sqrt()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (double)_sqrtExpression.Evaluate(_contexts[i])! > 0;

        return result;
    }

    // =========================================================================
    // COLLATOR
    // =========================================================================

    [Benchmark]
    public bool Equals_WithCollator()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_collatorEquals.Evaluate(_contexts[i])!;

        return result;
    }

    // =========================================================================
    // SINGLE CONTEXT BENCHMARKS (for micro-optimization testing)
    // =========================================================================

    [Benchmark]
    public bool Equals_String_SingleContext()
    {
        return (bool)_equalsString.Evaluate(_contextWithAllAttributes)!;
    }

    [Benchmark]
    public bool In_LargeList_SingleContext()
    {
        return (bool)_inLarge.Evaluate(_contextWithAllAttributes)!;
    }

    [Benchmark]
    public bool All_SingleContext()
    {
        return (bool)_allExpression.Evaluate(_contextWithAllAttributes)!;
    }

    // =========================================================================
    // EDGE CASE BENCHMARKS
    // =========================================================================

    [Benchmark]
    public bool MissingAttributes()
    {
        bool result = false;
        result ^= _equalsString.Evaluate(_contextWithoutOptionalAttributes) != null;
        result ^= _inSmall.Evaluate(_contextWithoutOptionalAttributes) != null;
        result ^= _hasFilterExisting.Evaluate(_contextWithoutOptionalAttributes) != null;
        result ^= _numberAssertion.Evaluate(_contextWithoutOptionalAttributes) != null;
        result ^= _allExpression.Evaluate(_contextWithoutOptionalAttributes) != null;
        return result;
    }

    [Benchmark]
    public bool MultipleContextsPerCall()
    {
        // Context creation per feature, as the renderer does with every feature
        bool result = false;

        for (int i = 0; i < _features.Length; i++)
            result ^= _getExpression.Evaluate(new EvaluationContext(14).WithFeature(_features[i])) != null;

        return result;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _features = null;
        _contexts = null;
    }
}
