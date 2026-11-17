using NetTopologySuite.Features;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Filter that checks if a feature has the specified attribute key.
/// </summary>
public class HasFilter : Filter
{
    /// <summary>
    /// The attribute key to check for existence.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Initializes a new has filter.
    /// </summary>
    /// <param name="key">The attribute key to check.</param>
    /// <exception cref="ArgumentNullException">Thrown if key is null or empty.</exception>
    public HasFilter(string key)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
    }

    /// <summary>
    /// Evaluates whether the feature has the specified attribute.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature has the specified attribute; otherwise, false.</returns>
    public override bool Evaluate(IFeature feature)
    {
        return feature != null && feature.Attributes.Exists(Key);
    }
}
