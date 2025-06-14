using NetTopologySuite.Features;
using VecTiles.Common.Interfaces;

namespace VecTiles.Styles.Mapbox.Filter;

public class AllFilter : CompoundFilter
{
    public AllFilter(List<IFilter> filters) : base(filters)
    {
    }

    public override bool Evaluate(IFeature feature)
    {
        foreach (var filter in Filters)
        {
            if (!filter.Evaluate(feature))
                return false;
        }

        return true;
    }
}
