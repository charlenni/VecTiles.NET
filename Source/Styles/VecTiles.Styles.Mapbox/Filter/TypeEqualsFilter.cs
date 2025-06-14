using NetTopologySuite.Features;
using VecTiles.Common.Enums;

namespace VecTiles.Styles.Mapbox.Filter;

public class TypeEqualsFilter : Filter
{
    public string Type { get; }

    public TypeEqualsFilter(GeometryType type)
    {
        Type = type.ToString() ?? string.Empty;
    }

    public override bool Evaluate(IFeature feature)
    {
        return feature != null && feature.Geometry.GeometryType.Equals(Type);
    }
}
