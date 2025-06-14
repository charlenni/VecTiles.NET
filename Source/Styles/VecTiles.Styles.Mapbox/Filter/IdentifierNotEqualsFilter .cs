using NetTopologySuite.Features;

namespace VecTiles.Styles.Mapbox.Filter;

public class IdentifierNotEqualsFilter : Filter
{
    public string Identifier { get; }

    public IdentifierNotEqualsFilter(string identifier)
    {
        Identifier = identifier;
    }

    public override bool Evaluate(IFeature feature)
    {
        return feature != null && feature.Attributes["id"].ToString() != Identifier;
    }
}
