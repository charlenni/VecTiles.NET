using System.Collections.Generic;
using NetTopologySuite.Features;
using VecTiles.Common.Enums;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Filter that checks if a feature's geometry type is NOT in the specified list of types.
/// Uses case-insensitive comparison and handles Multi* types.
/// </summary>
public class TypeNotInFilter : Filter
{
    /// <summary>
    /// The list of geometry types to exclude (all lower-case for case-insensitive comparison).
    /// </summary>
    public IList<string> Types { get; }

    /// <summary>
    /// Save Count for later use.
    /// </summary>
    private int _typesCount = 0;

    /// <summary>
    /// Initializes a new type not in filter.
    /// </summary>
    /// <param name="types">The collection of geometry types to exclude.</param>
    public TypeNotInFilter(IEnumerable<GeometryType> types)
    {
        Types = new List<string>();

        if (types != null)
        {
            foreach (var type in types)
            {
                var typeString = type.ToString()?.ToLowerInvariant();
                if (!string.IsNullOrEmpty(typeString))
                    Types.Add(typeString);
            }

            _typesCount = Types.Count;
        }
    }

    /// <summary>
    /// Evaluates whether the feature's geometry type is not in the list.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature's geometry type does not contain any of the specified types (case-insensitive); otherwise, false.</returns>
    public override bool Evaluate(IFeature feature)
    {
        if (feature == null || feature.Geometry == null)
            return true;

        var geometryType = feature.Geometry.GeometryType;
        if (string.IsNullOrEmpty(geometryType))
            return true;

        var lowerGeometryType = geometryType.ToLowerInvariant();

        for (var i = 0; i < _typesCount; i++)
        {
            // Use case-insensitive contains check
            if (lowerGeometryType.Contains(Types[i]))
                return false;
        }

        return true;
    }
}
