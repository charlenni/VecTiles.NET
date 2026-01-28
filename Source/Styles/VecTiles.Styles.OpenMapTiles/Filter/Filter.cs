using NetTopologySuite.Features;
using VecTiles.Common.Interfaces;

namespace VecTiles.Styles.OpenMapTiles.Filter;

public abstract class Filter : IFilter
{
    public abstract bool Evaluate(IFeature feature);
}
