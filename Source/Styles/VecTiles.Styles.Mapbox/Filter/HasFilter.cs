using NetTopologySuite.Features;

namespace VecTiles.Styles.Mapbox.Filter;

public class HasFilter : Filter
{
    public string Key { get; }

    public HasFilter(string key)
    {
        Key = key;
    }

    public override bool Evaluate(IFeature feature)
    {
        return feature != null && feature.Attributes.Exists(Key);
    }
}
