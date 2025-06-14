using NetTopologySuite.Features;

namespace VecTiles.Styles.Mapbox.Filter;

public class NullFilter : Filter
{
    public override bool Evaluate(IFeature feature)
    {
        return true;
    }
}
