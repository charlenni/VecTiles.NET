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

    /// <summary>
    /// Parses all benchmark expressions from their JSON sources and creates
    /// the test features together with a matching evaluation context per
    /// feature.
    /// </summary>
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

    /// <summary>
    /// Evaluates a get expression for the attribute "name" once per test
    /// context. This is the baseline of the benchmark run.
    /// </summary>
    /// <returns>True if the expression evaluated to a non-null value for at least one context.</returns>
    [Benchmark(Baseline = true)]
    public bool Get()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= _getExpression.Evaluate(_contexts[i]) != null;

        return result;
    }

    /// <summary>Evaluates a literal string expression once per test context.</summary>
    /// <returns>True if the expression evaluated to a non-null value for at least one context.</returns>
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

    /// <summary>Evaluates an "==" comparison against a string attribute, the future replacement of EqualityFilter.</summary>
    /// <returns>The XOR checksum of the per-context comparison results.</returns>
    [Benchmark]
    public bool Equals_String()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_equalsString.Evaluate(_contexts[i])!;

        return result;
    }

    /// <summary>Evaluates an "==" comparison against an integer attribute, the future replacement of EqualityFilter.</summary>
    /// <returns>The XOR checksum of the per-context comparison results.</returns>
    [Benchmark]
    public bool Equals_Int()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_equalsInt.Evaluate(_contexts[i])!;

        return result;
    }

    /// <summary>Evaluates a "!=" comparison against a string attribute, the future replacement of EqualityFilter.</summary>
    /// <returns>The XOR checksum of the per-context comparison results.</returns>
    [Benchmark]
    public bool NotEquals_String()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_notEqualsString.Evaluate(_contexts[i])!;

        return result;
    }

    /// <summary>Evaluates a "&lt;" comparison against a float attribute, the future replacement of ComparisonFilter.</summary>
    /// <returns>The XOR checksum of the per-context comparison results.</returns>
    [Benchmark]
    public bool LessThan_Float()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_lessThanFloat.Evaluate(_contexts[i])!;

        return result;
    }

    /// <summary>Evaluates a "&gt;" comparison against a float attribute, the future replacement of ComparisonFilter.</summary>
    /// <returns>The XOR checksum of the per-context comparison results.</returns>
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

    /// <summary>Evaluates an "in" expression against a small literal list (10 entries), the future replacement of InFilter.</summary>
    /// <returns>The XOR checksum of the per-context membership results.</returns>
    [Benchmark]
    public bool In_SmallList()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_inSmall.Evaluate(_contexts[i])!;

        return result;
    }

    /// <summary>Evaluates an "in" expression against a medium literal list (100 entries), the future replacement of InFilter.</summary>
    /// <returns>The XOR checksum of the per-context membership results.</returns>
    [Benchmark]
    public bool In_MediumList()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_inMedium.Evaluate(_contexts[i])!;

        return result;
    }

    /// <summary>Evaluates an "in" expression against a large literal list (1000 entries), the future replacement of InFilter including its HashSet optimization.</summary>
    /// <returns>The XOR checksum of the per-context membership results.</returns>
    [Benchmark]
    public bool In_LargeList()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_inLarge.Evaluate(_contexts[i])!;

        return result;
    }

    /// <summary>Evaluates an "in" expression against a small literal list of numbers.</summary>
    /// <returns>The XOR checksum of the per-context membership results.</returns>
    [Benchmark]
    public bool In_NumberList()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_inSmallNumbers.Evaluate(_contexts[i])!;

        return result;
    }

    /// <summary>Evaluates a "has" expression for an attribute that exists on every test feature.</summary>
    /// <returns>The XOR checksum of the per-context results.</returns>
    [Benchmark]
    public bool Has_Existing()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_hasFilterExisting.Evaluate(_contexts[i])!;

        return result;
    }

    /// <summary>Evaluates a "has" expression for an attribute that is missing on every test feature.</summary>
    /// <returns>The XOR checksum of the per-context results.</returns>
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

    /// <summary>Evaluates an "all" expression with three conditions that short-circuits like the AllFilter family.</summary>
    /// <returns>The XOR checksum of the per-context results.</returns>
    [Benchmark]
    public bool All()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_allExpression.Evaluate(_contexts[i])!;

        return result;
    }

    /// <summary>Evaluates an "any" expression with three conditions that short-circuits on the first match.</summary>
    /// <returns>The XOR checksum of the per-context results.</returns>
    [Benchmark]
    public bool Any()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (bool)_anyExpression.Evaluate(_contexts[i])!;

        return result;
    }

    /// <summary>Evaluates a "!" expression that negates a boolean coercion of a string attribute.</summary>
    /// <returns>The XOR checksum of the per-context results.</returns>
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

    /// <summary>Evaluates a "match" expression that dispatches on a string attribute.</summary>
    /// <returns>The XOR checksum of the per-context result string lengths.</returns>
    [Benchmark]
    public bool Match()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= ((string)_matchExpression.Evaluate(_contexts[i])!).Length > 0;

        return result;
    }

    /// <summary>Evaluates a "case" expression whose condition is an "==" comparison on a string attribute.</summary>
    /// <returns>The XOR checksum of the per-context result string lengths.</returns>
    [Benchmark]
    public bool Case()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= ((string)_caseExpression.Evaluate(_contexts[i])!).Length > 0;

        return result;
    }

    /// <summary>Evaluates a "coalesce" expression that returns the first non-null attribute.</summary>
    /// <returns>The XOR checksum of the per-context results.</returns>
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

    /// <summary>Evaluates a "step" expression that maps the zoom level to discrete stops.</summary>
    /// <returns>The XOR checksum of the per-context results.</returns>
    [Benchmark]
    public bool Step()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (double)_stepExpression.Evaluate(_contexts[i])! > 0;

        return result;
    }

    /// <summary>Evaluates an "interpolate" expression with exponential base between zoom stops.</summary>
    /// <returns>The XOR checksum of the per-context results.</returns>
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

    /// <summary>Evaluates a "number" type assertion on an attribute.</summary>
    /// <returns>The XOR checksum of the per-context results.</returns>
    [Benchmark]
    public bool NumberAssertion()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= _numberAssertion.Evaluate(_contexts[i]) != null;

        return result;
    }

    /// <summary>Evaluates a "to-string" coercion of an integer attribute.</summary>
    /// <returns>The XOR checksum of the per-context results.</returns>
    [Benchmark]
    public bool ToString_()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (string)_toStringExpression.Evaluate(_contexts[i])! != null;

        return result;
    }

    /// <summary>Evaluates a "concat" expression that joins a literal with an attribute value.</summary>
    /// <returns>The XOR checksum of the per-context result string lengths.</returns>
    [Benchmark]
    public bool Concat()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (_concatExpression.Evaluate(_contexts[i]) as string)!.Length > 0;

        return result;
    }

    /// <summary>Evaluates an "upcase" expression on a string attribute.</summary>
    /// <returns>The XOR checksum of the per-context result string lengths.</returns>
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

    /// <summary>Evaluates a "+" arithmetic expression over two attributes and a literal.</summary>
    /// <returns>The XOR checksum of the per-context results.</returns>
    [Benchmark]
    public bool Plus()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (double)_plusExpression.Evaluate(_contexts[i])! > 0;

        return result;
    }

    /// <summary>Evaluates a "min" expression over an attribute and a literal.</summary>
    /// <returns>The XOR checksum of the per-context results.</returns>
    [Benchmark]
    public bool Min()
    {
        bool result = false;

        for (int i = 0; i < _contexts.Length; i++)
            result ^= (double)_minExpression.Evaluate(_contexts[i])! >= 0;

        return result;
    }

    /// <summary>Evaluates a "sqrt" expression on an attribute.</summary>
    /// <returns>The XOR checksum of the per-context results.</returns>
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

    /// <summary>Evaluates an "==" comparison with a case-sensitive collator on a string attribute.</summary>
    /// <returns>The XOR checksum of the per-context results.</returns>
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

    /// <summary>Evaluates the string "==" expression once on a single context, for micro-optimization testing.</summary>
    /// <returns>The comparison result for the single context.</returns>
    [Benchmark]
    public bool Equals_String_SingleContext()
    {
        return (bool)_equalsString.Evaluate(_contextWithAllAttributes)!;
    }

    /// <summary>Evaluates the large-list "in" expression once on a single context, for micro-optimization testing.</summary>
    /// <returns>The membership result for the single context.</returns>
    [Benchmark]
    public bool In_LargeList_SingleContext()
    {
        return (bool)_inLarge.Evaluate(_contextWithAllAttributes)!;
    }

    /// <summary>Evaluates the "all" expression once on a single context, for micro-optimization testing.</summary>
    /// <returns>The evaluation result for the single context.</returns>
    [Benchmark]
    public bool All_SingleContext()
    {
        return (bool)_allExpression.Evaluate(_contextWithAllAttributes)!;
    }

    // =========================================================================
    // EDGE CASE BENCHMARKS
    // =========================================================================

    /// <summary>Evaluates several expressions against a feature without optional attributes, to measure the cost of missing attributes.</summary>
    /// <returns>The XOR checksum of the evaluation results.</returns>
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

    /// <summary>Creates a new evaluation context per feature, as the renderer does for every feature, and evaluates the get expression for each.</summary>
    /// <returns>The XOR checksum of the evaluation results.</returns>
    [Benchmark]
    public bool MultipleContextsPerCall()
    {
        // Context creation per feature, as the renderer does with every feature
        bool result = false;

        for (int i = 0; i < _features.Length; i++)
            result ^= _getExpression.Evaluate(new EvaluationContext(14).WithFeature(_features[i])) != null;

        return result;
    }

    /// <summary>Clears the benchmark state.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _features = null;
        _contexts = null;
    }
}
