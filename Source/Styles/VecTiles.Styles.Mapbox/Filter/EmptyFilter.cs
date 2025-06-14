using NetTopologySuite.Features;

namespace VecTiles.Styles.Mapbox.Filter;

public class EmptyFilter : Filter
{
    public override bool Evaluate(IFeature feature)
    {
        return true;
    }
}
