using NetTopologySuite.Features;
using VecTiles.Common.Enums;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Filter that checks if a feature's geometry type matches the specified type.
/// Uses case-insensitive comparison and handles Multi* types (e.g., MultiLineString contains LineString).
/// </summary>
public class TypeEqualsFilter : Filter
{
    /// <summary>
    /// The geometry type to match (lower-case for case-insensitive comparison).
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Initializes a new type equals filter.
    /// </summary>
    /// <param name="type">The geometry type to match.</param>
    public TypeEqualsFilter(GeometryType type)
    {
        Type = type.ToString()?.ToLowerInvariant() ?? string.Empty;
    }

    /// <summary>
    /// Evaluates whether the feature's geometry type matches.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature's geometry type contains the specified type (case-insensitive); otherwise, false.</returns>
    public override bool Evaluate(IFeature feature)
    {
        if (feature == null || feature.Geometry == null)
            return false;

        var geometryType = feature.Geometry.GeometryType;
        if (string.IsNullOrEmpty(geometryType))
            return false;

        // Use case-insensitive contains check
        return geometryType.ToLowerInvariant().Contains(Type);
    }
}
