using NetTopologySuite.Features;
using VecTiles.Common.Enums;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Filter that checks if a feature's geometry type does NOT match the specified type.
/// Uses case-insensitive comparison and handles Multi* types.
/// </summary>
public class TypeNotEqualsFilter : Filter
{
    /// <summary>
    /// The geometry type to exclude (lower-case for case-insensitive comparison).
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Initializes a new type not equals filter.
    /// </summary>
    /// <param name="type">The geometry type to exclude.</param>
    public TypeNotEqualsFilter(GeometryType type)
    {
        Type = type.ToString()?.ToLowerInvariant() ?? string.Empty;
    }

    /// <summary>
    /// Evaluates whether the feature's geometry type does not match.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature's geometry type does not contain the specified type (case-insensitive); otherwise, false.</returns>
    public override bool Evaluate(IFeature feature)
    {
        if (feature == null || feature.Geometry == null)
            return false;

        var geometryType = feature.Geometry.GeometryType;
        if (string.IsNullOrEmpty(geometryType))
            return false;

        // Use case-insensitive contains check
        return !geometryType.ToLowerInvariant().Contains(Type);
    }
}
