using NetTopologySuite.Features;

namespace VecTiles.Styles.Mapbox.Filter;

public class IdentifierInFilter : Filter
{
    public IList<string> Identifiers { get; }

    public IdentifierInFilter(IEnumerable<string> identifiers)
    {
        Identifiers = new List<string>();

        foreach (var identifier in identifiers)
            Identifiers.Add(identifier);
    }

    public override bool Evaluate(IFeature feature)
    {
        if (feature == null)
            return false;

        foreach (var identifier in Identifiers)
            if (feature.Attributes["id"].ToString() == identifier)
                return true;

        return false;
    }
}
