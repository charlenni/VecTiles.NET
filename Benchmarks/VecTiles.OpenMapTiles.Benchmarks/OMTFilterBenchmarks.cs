using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Styles.OpenMapTiles.Filter;

namespace VecTiles.OpenMapTiles.Benchmarks;

/// <summary>
/// Benchmark tests for all OpenMapTiles filter classes.
/// Filters are called for every single feature, so performance is critical.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class OMTFilterBenchmarks
{
    // Test data configurations
    private const int SmallListSize = 10;
    private const int MediumListSize = 100;
    private const int LargeListSize = 1000;
    private const int FeatureCount = 1000;

    // Test features
    private IFeature[] _features;
    private IFeature _featureWithAllAttributes;
    private IFeature _featureWithNumericAttributes;
    private IFeature _featureWithoutOptionalAttributes;

    // Filter instances - created once and reused
    private EqualsFilter _equalsFilterString;
    private EqualsFilter _equalsFilterInt;
    private NotEqualsFilter _notEqualsFilterString;
    private HasFilter _hasFilterExisting;
    private HasFilter _hasFilterMissing;
    private NotHasFilter _notHasFilterExisting;
    private NotHasFilter _notHasFilterMissing;

    private GreaterThanFilter _greaterThanFilterFloat;
    private GreaterThanFilter _greaterThanFilterInt;
    private GreaterThanEqualsFilter _greaterThanEqualsFilter;
    private LessThanFilter _lessThanFilter;
    private LessThanEqualsFilter _lessThanEqualsFilter;

    private InFilter _inFilterSmall;
    private InFilter _inFilterMedium;
    private InFilter _inFilterLarge;
    private NotInFilter _notInFilterSmall;
    private NotInFilter _notInFilterMedium;
    private NotInFilter _notInFilterLarge;

    private TypeEqualsFilter _typeEqualsFilterPoint;
    private TypeEqualsFilter _typeEqualsFilterLineString;
    private TypeNotEqualsFilter _typeNotEqualsFilter;
    private TypeInFilter _typeInFilter;
    private TypeNotInFilter _typeNotInFilter;

    private IdentifierEqualsFilter _identifierEqualsFilter;
    private IdentifierNotEqualsFilter _identifierNotEqualsFilter;
    private IdentifierInFilter _identifierInFilterSmall;
    private IdentifierInFilter _identifierInFilterMedium;
    private IdentifierNotInFilter _identifierNotInFilterSmall;

    private HasIdentifierFilter _hasIdentifierFilter;
    private NotHasIdentifierFilter _notHasIdentifierFilter;

    private AllFilter _allFilter;
    private AnyFilter _anyFilter;
    private NoneFilter _noneFilter;

    private EmptyFilter _emptyFilter;
    private ExpressionFilter _expressionFilter;

    // Compound filter components
    private EqualsFilter _compoundFilter1;
    private HasFilter _compoundFilter2;
    private GreaterThanFilter _compoundFilter3;

    [GlobalSetup]
    public void Setup()
    {
        // Create test features
        _features = CreateTestFeatures(FeatureCount);
        _featureWithAllAttributes = CreateFeatureWithAllAttributes();
        _featureWithNumericAttributes = CreateFeatureWithNumericAttributes();
        _featureWithoutOptionalAttributes = CreateFeatureWithoutOptionalAttributes();

        // Create simple filters
        _equalsFilterString = new EqualsFilter("name", "TestFeature");
        _equalsFilterInt = new EqualsFilter("level", 5);
        _notEqualsFilterString = new NotEqualsFilter("name", "OtherFeature");
        _hasFilterExisting = new HasFilter("name");
        _hasFilterMissing = new HasFilter("nonexistent");
        _notHasFilterExisting = new NotHasFilter("name");
        _notHasFilterMissing = new NotHasFilter("nonexistent");

        // Create numeric filters
        _greaterThanFilterFloat = new GreaterThanFilter("elevation", 100.5f);
        _greaterThanFilterInt = new GreaterThanFilter("level", 3);
        _greaterThanEqualsFilter = new GreaterThanEqualsFilter("elevation", 100.0f);
        _lessThanFilter = new LessThanFilter("elevation", 200.0f);
        _lessThanEqualsFilter = new LessThanEqualsFilter("elevation", 200.0f);

        // Create in filters with different list sizes
        var smallStrings = Enumerable.Range(0, SmallListSize).Select(i => $"value_{i}").ToList();
        var mediumStrings = Enumerable.Range(0, MediumListSize).Select(i => $"value_{i}").ToList();
        var largeStrings = Enumerable.Range(0, LargeListSize).Select(i => $"value_{i}").ToList();

        _inFilterSmall = new InFilter("type", smallStrings);
        _inFilterMedium = new InFilter("type", mediumStrings);
        _inFilterLarge = new InFilter("type", largeStrings);
        _notInFilterSmall = new NotInFilter("type", smallStrings);
        _notInFilterMedium = new NotInFilter("type", mediumStrings);
        _notInFilterLarge = new NotInFilter("type", largeStrings);

        // Create type filters
        _typeEqualsFilterPoint = new TypeEqualsFilter(GeometryType.Point);
        _typeEqualsFilterLineString = new TypeEqualsFilter(GeometryType.LineString);
        _typeNotEqualsFilter = new TypeNotEqualsFilter(GeometryType.Point);
        _typeInFilter = new TypeInFilter(new[] { GeometryType.Point, GeometryType.LineString });
        _typeNotInFilter = new TypeNotInFilter(new[] { GeometryType.Point, GeometryType.LineString });

        // Create identifier filters
        var smallIds = Enumerable.Range(0, SmallListSize).Select(i => $"id_{i}").ToList();
        var mediumIds = Enumerable.Range(0, MediumListSize).Select(i => $"id_{i}").ToList();

        _identifierEqualsFilter = new IdentifierEqualsFilter("feature_1");
        _identifierNotEqualsFilter = new IdentifierNotEqualsFilter("feature_1");
        _identifierInFilterSmall = new IdentifierInFilter(smallIds);
        _identifierInFilterMedium = new IdentifierInFilter(mediumIds);
        _identifierNotInFilterSmall = new IdentifierNotInFilter(smallIds);

        // Create identifier existence filters
        _hasIdentifierFilter = new HasIdentifierFilter();
        _notHasIdentifierFilter = new NotHasIdentifierFilter();

        // Create compound filters
        _compoundFilter1 = new EqualsFilter("name", "TestFeature");
        _compoundFilter2 = new HasFilter("level");
        _compoundFilter3 = new GreaterThanFilter("elevation", 50.0f);

        _allFilter = new AllFilter(new List<IFilter> { _compoundFilter1, _compoundFilter2, _compoundFilter3 });
        _anyFilter = new AnyFilter(new List<IFilter> { _compoundFilter1, _compoundFilter2, _compoundFilter3 });
        _noneFilter = new NoneFilter(new List<IFilter> { _compoundFilter1, _compoundFilter2, _compoundFilter3 });

        // Create simple filters
        _emptyFilter = new EmptyFilter();
        _expressionFilter = new ExpressionFilter();
    }

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

        var geometry = new Point(0, 0);
        return new Feature(geometry, attributes);
    }

    private IFeature CreateFeatureWithNumericAttributes()
    {
        var attributes = new AttributesTable();
        attributes.Add("id", "numeric_feature");
        attributes.Add("level", 10);
        attributes.Add("elevation", 250.0f);
        attributes.Add("population", 1000L);
        attributes.Add("temperature", -5.5);

        var geometry = new Point(1, 1);
        return new Feature(geometry, attributes);
    }

    private IFeature CreateFeatureWithoutOptionalAttributes()
    {
        var attributes = new AttributesTable();
        attributes.Add("id", "minimal_feature");

        var geometry = new LineString(new[] { new Coordinate(0, 0), new Coordinate(1, 1) });
        return new Feature(geometry, attributes);
    }

    // =========================================================================
    // SIMPLE FILTER BENCHMARKS
    // =========================================================================

    [Benchmark]
    public bool EqualsFilter_String()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _equalsFilterString.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool EqualsFilter_Int()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _equalsFilterInt.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool NotEqualsFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _notEqualsFilterString.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool HasFilter_Existing()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _hasFilterExisting.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool HasFilter_Missing()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _hasFilterMissing.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool NotHasFilter_Existing()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _notHasFilterExisting.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool NotHasFilter_Missing()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _notHasFilterMissing.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool EmptyFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _emptyFilter.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool ExpressionFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _expressionFilter.Evaluate(feature);
        }
        return result;
    }

    // =========================================================================
    // NUMERIC FILTER BENCHMARKS
    // =========================================================================

    [Benchmark]
    public bool GreaterThanFilter_Float()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _greaterThanFilterFloat.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool GreaterThanFilter_Int()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _greaterThanFilterInt.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool GreaterThanEqualsFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _greaterThanEqualsFilter.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool LessThanFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _lessThanFilter.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool LessThanEqualsFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _lessThanEqualsFilter.Evaluate(feature);
        }
        return result;
    }

    // =========================================================================
    // IN FILTER BENCHMARKS (with different list sizes)
    // =========================================================================

    [Benchmark]
    public bool InFilter_SmallList()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _inFilterSmall.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool InFilter_MediumList()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _inFilterMedium.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool InFilter_LargeList()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _inFilterLarge.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool NotInFilter_SmallList()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _notInFilterSmall.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool NotInFilter_MediumList()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _notInFilterMedium.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool NotInFilter_LargeList()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _notInFilterLarge.Evaluate(feature);
        }
        return result;
    }

    // =========================================================================
    // TYPE FILTER BENCHMARKS
    // =========================================================================

    [Benchmark]
    public bool TypeEqualsFilter_Point()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _typeEqualsFilterPoint.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool TypeEqualsFilter_LineString()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _typeEqualsFilterLineString.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool TypeNotEqualsFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _typeNotEqualsFilter.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool TypeInFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _typeInFilter.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool TypeNotInFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _typeNotInFilter.Evaluate(feature);
        }
        return result;
    }

    // =========================================================================
    // IDENTIFIER FILTER BENCHMARKS
    // =========================================================================

    [Benchmark]
    public bool IdentifierEqualsFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _identifierEqualsFilter.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool IdentifierNotEqualsFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _identifierNotEqualsFilter.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool IdentifierInFilter_SmallList()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _identifierInFilterSmall.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool IdentifierInFilter_MediumList()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _identifierInFilterMedium.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool IdentifierNotInFilter_SmallList()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _identifierNotInFilterSmall.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool HasIdentifierFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _hasIdentifierFilter.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool NotHasIdentifierFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _notHasIdentifierFilter.Evaluate(feature);
        }
        return result;
    }

    // =========================================================================
    // COMPOUND FILTER BENCHMARKS
    // =========================================================================

    [Benchmark]
    public bool AllFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _allFilter.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool AnyFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _anyFilter.Evaluate(feature);
        }
        return result;
    }

    [Benchmark]
    public bool NoneFilter()
    {
        bool result = false;
        foreach (var feature in _features)
        {
            result ^= _noneFilter.Evaluate(feature);
        }
        return result;
    }

    // =========================================================================
    // SINGLE FEATURE BENCHMARKS (for micro-optimization testing)
    // =========================================================================

    [Benchmark]
    public bool EqualsFilter_SingleFeature()
    {
        return _equalsFilterString.Evaluate(_featureWithAllAttributes);
    }

    [Benchmark]
    public bool GreaterThanFilter_SingleFeature()
    {
        return _greaterThanFilterFloat.Evaluate(_featureWithNumericAttributes);
    }

    [Benchmark]
    public bool InFilter_SingleFeature_SmallList()
    {
        return _inFilterSmall.Evaluate(_featureWithAllAttributes);
    }

    [Benchmark]
    public bool InFilter_SingleFeature_LargeList()
    {
        return _inFilterLarge.Evaluate(_featureWithAllAttributes);
    }

    [Benchmark]
    public bool TypeEqualsFilter_SingleFeature()
    {
        return _typeEqualsFilterPoint.Evaluate(_featureWithAllAttributes);
    }

    [Benchmark]
    public bool AllFilter_SingleFeature()
    {
        return _allFilter.Evaluate(_featureWithAllAttributes);
    }

    // =========================================================================
    // EDGE CASE BENCHMARKS
    // =========================================================================

    [Benchmark]
    public bool AllFilters_NullFeature()
    {
        bool result = false;
        result ^= _equalsFilterString.Evaluate(null);
        result ^= _hasFilterExisting.Evaluate(null);
        result ^= _greaterThanFilterFloat.Evaluate(null);
        result ^= _inFilterSmall.Evaluate(null);
        result ^= _typeEqualsFilterPoint.Evaluate(null);
        result ^= _identifierEqualsFilter.Evaluate(null);
        result ^= _allFilter.Evaluate(null);
        result ^= _emptyFilter.Evaluate(null);
        return result;
    }

    [Benchmark]
    public bool AllFilters_MissingAttributes()
    {
        bool result = false;
        result ^= _equalsFilterString.Evaluate(_featureWithoutOptionalAttributes);
        result ^= _hasFilterExisting.Evaluate(_featureWithoutOptionalAttributes);
        result ^= _greaterThanFilterFloat.Evaluate(_featureWithoutOptionalAttributes);
        result ^= _inFilterSmall.Evaluate(_featureWithoutOptionalAttributes);
        result ^= _typeEqualsFilterPoint.Evaluate(_featureWithoutOptionalAttributes);
        result ^= _identifierEqualsFilter.Evaluate(_featureWithoutOptionalAttributes);
        return result;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Clean up resources if needed
        _features = null;
    }
}
