using NetTopologySuite.Features;
using VecTiles.Common.Interfaces;

namespace VecTiles.Styles.Mapbox.Filter;

public abstract class CompoundFilter : Filter
{
    public List<IFilter> Filters { get; }

    public CompoundFilter()
    {
        Filters = new List<IFilter>();
    }

    public CompoundFilter(List<IFilter> filters)
    {
        Filters = new List<IFilter>();

        if (filters == null)
            return;

        foreach (var filter in filters)
            Filters.Add(filter);
    }

    public abstract override bool Evaluate(IFeature feature);
}
