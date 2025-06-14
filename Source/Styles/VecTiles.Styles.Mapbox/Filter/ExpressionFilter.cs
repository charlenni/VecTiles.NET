using NetTopologySuite.Features;

namespace VecTiles.Styles.Mapbox.Filter;

public class ExpressionFilter : Filter
{
    public override bool Evaluate(IFeature feature)
    {
        return false;
    }
}
