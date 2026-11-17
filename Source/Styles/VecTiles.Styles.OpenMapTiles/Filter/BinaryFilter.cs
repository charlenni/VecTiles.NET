using NetTopologySuite.Features;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Base class for binary comparison filters (>, <, >=, <=).
/// Provides common comparison logic for numeric types.
/// </summary>
public abstract class BinaryFilter : Filter
{
    /// <summary>
    /// The attribute key to compare against.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The value to compare with the feature's attribute.
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// Initializes a new binary filter. Protected for derived classes only.
    /// </summary>
    protected BinaryFilter()
    {
    }

    /// <summary>
    /// Initializes a new binary filter with the specified key and value.
    /// </summary>
    /// <param name="key">The attribute key to compare.</param>
    /// <param name="value">The value to compare against.</param>
    /// <exception cref="ArgumentNullException">Thrown if key is null or empty.</exception>
    public BinaryFilter(string key, object value)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Value = value;
    }

    /// <summary>
    /// Gets the numeric value of the feature's attribute, or null if not numeric.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>The numeric value as double, or null if the attribute is not numeric or doesn't exist.</returns>
    protected double? GetNumericValue(IFeature feature)
    {
        if (feature == null || !feature.Attributes.Exists(Key))
            return null;

        var attrValue = feature.Attributes[Key];

        // Handle all numeric types
        switch (attrValue)
        {
            case float f:
                return f;
            case double d:
                return d;
            case int i:
                return i;
            case long l:
                return l;
            case decimal m:
                return (double)m;
            case short s:
                return s;
            case byte b:
                return b;
            case uint ui:
                return ui;
            case ulong ul:
                return ul;
            default:
                return null;
        }
    }

    /// <summary>
    /// Gets the numeric value to compare against, or null if not numeric.
    /// </summary>
    /// <returns>The numeric value as double, or null if the value is not numeric.</returns>
    protected double? GetNumericCompareValue()
    {
        switch (Value)
        {
            case float f:
                return f;
            case double d:
                return d;
            case int i:
                return i;
            case long l:
                return l;
            case decimal m:
                return (double)m;
            case short s:
                return s;
            case byte b:
                return b;
            case uint ui:
                return ui;
            case ulong ul:
                return ul;
            default:
                return null;
        }
    }

    /// <summary>
    /// Evaluates whether the feature passes this binary comparison.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature passes the filter; otherwise, false.</returns>
    public abstract override bool Evaluate(IFeature feature);
}
