using NetTopologySuite.Features;
using VecTiles.Common.Interfaces;

namespace VecTiles.Styles.Mapbox.Filter;

public class AnyFilter : CompoundFilter
{
    public AnyFilter(List<IFilter> filters) : base(filters)
    {
    }

    public override bool Evaluate(IFeature feature)
    {
        foreach (var filter in Filters)
        {
            if (filter.Evaluate(feature))
            {
                return true;
            }
        }

        return false;
    }
}
