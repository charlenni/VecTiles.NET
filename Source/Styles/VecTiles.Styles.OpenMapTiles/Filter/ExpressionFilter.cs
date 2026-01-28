using NetTopologySuite.Features;

namespace VecTiles.Styles.OpenMapTiles.Filter;

public class ExpressionFilter : Filter
{
    public override bool Evaluate(IFeature feature)
    {
        return false;
    }
}
