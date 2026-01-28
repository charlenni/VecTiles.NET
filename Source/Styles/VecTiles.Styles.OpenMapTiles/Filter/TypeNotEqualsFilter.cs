using NetTopologySuite.Features;
using VecTiles.Common.Enums;

namespace VecTiles.Styles.OpenMapTiles.Filter;

public class TypeNotEqualsFilter : Filter
{
    public string Type { get; }

    public TypeNotEqualsFilter(GeometryType type)
    {
        Type = type.ToString();
    }

    public override bool Evaluate(IFeature feature)
    {
        // Use Contains instead of Equals because of e.g. LineString and MultiLineString
        return feature != null && !feature.Geometry.GeometryType.Contains(Type);
    }
}
