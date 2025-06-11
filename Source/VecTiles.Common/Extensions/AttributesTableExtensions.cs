using NetTopologySuite.Features;

namespace VecTiles.Common.Extensions;

/// <summary>
/// Extension methods for <see cref="IAttributesTable"/> to simplify attribute lookups.
/// </summary>
public static class AttributesTableExtensions
{
    /// <summary>
    /// Returns true if the given key-value pair is found in this tags collection.
    /// </summary>
    /// <param name="key">The attribute key to look for.</param>
    /// <param name="value">The value to compare against the attribute value.</param>
    /// <returns>True if the key exists and its value equals the specified value; otherwise, false.</returns>
    public static bool ContainsKeyValue(this IAttributesTable attributes, string key, object value)
    {
        if (!attributes.Exists(key))
            return false;

        return attributes[key].Equals(value);
    }
}
