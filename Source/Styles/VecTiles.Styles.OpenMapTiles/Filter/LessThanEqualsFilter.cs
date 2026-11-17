using NetTopologySuite.Features;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Filter that checks if a feature's numeric attribute is less than or equal to the specified value.
/// Supports all numeric types (float, double, int, long, decimal, etc.).
/// </summary>
public class LessThanEqualsFilter : BinaryFilter
{
    /// <summary>
    /// Initializes a new less than or equals filter.
    /// </summary>
    /// <param name="key">The attribute key to compare.</param>
    /// <param name="value">The value to compare against.</param>
    /// <exception cref="ArgumentNullException">Thrown if key is null or empty.</exception>
    public LessThanEqualsFilter(string key, object value) : base(key, value)
    {
    }

    /// <summary>
    /// Evaluates whether the feature's attribute is less than or equal to the specified value.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature's attribute is less than or equal to the specified value; otherwise, false.</returns>
    public override bool Evaluate(IFeature feature)
    {
        var attrValue = GetNumericValue(feature);
        var compareValue = GetNumericCompareValue();

        if (attrValue == null || compareValue == null)
            return false;

        return attrValue.Value <= compareValue.Value;
    }
}
