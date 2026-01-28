using NetTopologySuite.Features;

namespace VecTiles.Styles.OpenMapTiles.Filter;

public class HasIdentifierFilter : Filter
{
    public HasIdentifierFilter()
    {
    }

    public override bool Evaluate(IFeature feature)
    {
        return feature != null && !string.IsNullOrWhiteSpace(feature.Attributes["id"].ToString());
    }
}
