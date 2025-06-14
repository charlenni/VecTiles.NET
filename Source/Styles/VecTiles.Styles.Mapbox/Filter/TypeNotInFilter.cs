using NetTopologySuite.Features;
using VecTiles.Common.Enums;

namespace VecTiles.Styles.Mapbox.Filter;

public class TypeNotInFilter : Filter
{
    public IList<string> Types { get; }

    public TypeNotInFilter(IEnumerable<GeometryType> types)
    {
        Types = new List<string>();

        foreach (var type in types)
            Types.Add(type.ToString());
    }

    public override bool Evaluate(IFeature feature)
    {
        if (feature == null)
            return true;

        foreach (var type in Types)
            if (feature.Geometry.GeometryType.Equals(type))
                return false;

        return true;
    }
}
