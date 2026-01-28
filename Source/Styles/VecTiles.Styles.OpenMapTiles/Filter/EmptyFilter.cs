using NetTopologySuite.Features;

namespace VecTiles.Styles.OpenMapTiles.Filter;

public class EmptyFilter : Filter
{
    public override bool Evaluate(IFeature feature)
    {
        return true;
    }
}
