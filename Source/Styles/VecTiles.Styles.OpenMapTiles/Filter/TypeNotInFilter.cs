using NetTopologySuite.Features;
using VecTiles.Common.Enums;

namespace VecTiles.Styles.OpenMapTiles.Filter;

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
            // Use Contains instead of Equals because of e.g. LineString and MultiLineString
            if (feature.Geometry.GeometryType.Contains(type))
                return false;

        return true;
    }
}
