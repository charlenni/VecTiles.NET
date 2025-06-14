using NetTopologySuite.Features;
using VecTiles.Common.Extensions;

namespace VecTiles.Styles.Mapbox.Filter;

public class EqualsFilter : Filter
{
    public string Key { get; }
    public object Value { get; }

    public EqualsFilter(string key, object value)
    {
        Key = key;
        Value = value;
    }
    
    public override bool Evaluate(IFeature feature)
    {
        return feature != null && feature.Attributes.ContainsKeyValue(Key, Value);
    }
}
