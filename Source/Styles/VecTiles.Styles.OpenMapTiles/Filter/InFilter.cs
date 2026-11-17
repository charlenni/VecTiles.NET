using System.Collections.Generic;
using NetTopologySuite.Features;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Filter that checks if a feature's attribute value is contained in the specified list of values.
/// Uses a HashSet for O(1) lookup performance when the list has more than 10 elements.
/// </summary>
public class InFilter : Filter
{
    /// <summary>
    /// The attribute key to check.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The list of values to check against.
    /// </summary>
    public IList<object> Values { get; }

    /// <summary>
    /// HashSet for O(1) lookups (created lazily when list has more than 10 elements).
    /// </summary>
    private HashSet<object> _valueSet;

    /// <summary>
    /// Save Count for later use.
    /// </summary>
    private int _valuesCount = 0;
    
    /// <summary>
    /// Initializes a new in filter.
    /// </summary>
    /// <param name="key">The attribute key to check.</param>
    /// <param name="values">The collection of values to match.</param>
    /// <exception cref="ArgumentNullException">Thrown if key is null or empty.</exception>
    public InFilter(string key, IEnumerable<object> values)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Values = new List<object>();

        if (values != null)
        {
            foreach (var value in values)
            {
                if (value != null)
                    Values.Add(value);
            }

            _valuesCount = Values.Count;
            
            // Create HashSet for O(1) lookups if we have more than 10 values
            if (_valuesCount > 10)
            {
                _valueSet = new HashSet<object>(Values);
            }
        }
    }

    /// <summary>
    /// Gets the HashSet for fast lookups, creating it if necessary.
    /// </summary>
    private HashSet<object> ValueSet
    {
        get
        {
            if (_valueSet == null && _valuesCount > 10)
            {
                _valueSet = new HashSet<object>(Values);
            }
            return _valueSet;
        }
    }

    /// <summary>
    /// Evaluates whether the feature's attribute value is in the list.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature's attribute value is in the specified list; otherwise, false.</returns>
    public override bool Evaluate(IFeature feature)
    {
        if (feature == null || !feature.Attributes.Exists(Key))
            return false;

        var value = feature.Attributes[Key];

        if (value == null)
            return false;

        // Use HashSet for O(1) lookup if we have more than 10 values
        if (_valuesCount > 10 && _valueSet != null)
        {
            return _valueSet.Contains(value);
        }

        // For small lists, linear search might be faster due to cache locality
        for (var i = 0; i < _valuesCount; i++)
        {
            if (Values[i].Equals(value))
                return true;
        }

        return false;
    }
}
