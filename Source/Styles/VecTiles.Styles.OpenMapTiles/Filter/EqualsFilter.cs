using NetTopologySuite.Features;
using VecTiles.Common.Extensions;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Filter that checks if a feature's attribute equals the specified value.
/// Uses <see cref="AttributesTableExtensions.ContainsKeyValue"/> for efficient comparison.
/// </summary>
public class EqualsFilter : Filter
{
    /// <summary>
    /// The attribute key to compare.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The value to compare against.
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// Initializes a new equals filter.
    /// </summary>
    /// <param name="key">The attribute key to compare.</param>
    /// <param name="value">The value to compare against.</param>
    /// <exception cref="ArgumentNullException">Thrown if key is null or empty.</exception>
    public EqualsFilter(string key, object value)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Value = value;
    }

    /// <summary>
    /// Evaluates whether the feature's attribute equals the specified value.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature has the specified attribute with the specified value; otherwise, false.</returns>
    public override bool Evaluate(IFeature feature)
    {
        return feature != null && feature.Attributes.ContainsKeyValue(Key, Value);
    }
}
