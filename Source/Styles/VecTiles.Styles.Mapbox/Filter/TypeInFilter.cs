using NetTopologySuite.Features;
using VecTiles.Common.Enums;

namespace VecTiles.Styles.Mapbox.Filter;

public class TypeInFilter : Filter
{
    public IList<string> Types { get; }

    public TypeInFilter(IEnumerable<GeometryType> types)
    {
        Types = new List<string>();

        foreach (var type in types)
            Types.Add(type.ToString());
    }

    public override bool Evaluate(IFeature feature)
    {
        if (feature == null)
            return false;

        foreach (var type in Types)
            if (feature.Geometry.GeometryType.Equals(type))
                return true;

        return false;
    }
}
