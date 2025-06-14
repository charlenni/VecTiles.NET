using NetTopologySuite.Features;
using VecTiles.Common.Interfaces;

namespace VecTiles.Styles.Mapbox.Filter;

public abstract class Filter : IFilter
{
    public abstract bool Evaluate(IFeature feature);
}
