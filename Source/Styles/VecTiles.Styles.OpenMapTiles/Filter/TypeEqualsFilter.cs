using NetTopologySuite.Features;
using VecTiles.Common.Enums;

namespace VecTiles.Styles.OpenMapTiles.Filter;

public class TypeEqualsFilter : Filter
{
    public string Type { get; }

    public TypeEqualsFilter(GeometryType type)
    {
        Type = type.ToString() ?? string.Empty;
    }

    public override bool Evaluate(IFeature feature)
    {
        // Use Contains instead of Equals because of e.g. LineString and MultiLineString
        return feature != null && feature.Geometry.GeometryType.Contains(Type);
    }
}
