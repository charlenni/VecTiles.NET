using NetTopologySuite.Features;

namespace VecTiles.Styles.Mapbox.Filter;

public class LessThanEqualsFilter : BinaryFilter
{
    public LessThanEqualsFilter(string key, object value) : base(key, value)
    {
    }

    public override bool Evaluate(IFeature feature)
    {
        if (feature == null || !feature.Attributes.Exists(Key))
            return false;

        if (feature.Attributes[Key] is float)
            return (float)feature.Attributes[Key] <= (float)Value;

        if (feature.Attributes[Key] is long)
            return (long)feature.Attributes[Key] <= (long)Value;

        return false;
    }
}
