using NetTopologySuite.Features;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Filter that checks if a feature does NOT have the specified attribute key.
/// </summary>
public class NotHasFilter : Filter
{
    /// <summary>
    /// The attribute key to check for non-existence.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Initializes a new not has filter.
    /// </summary>
    /// <param name="key">The attribute key to check.</param>
    /// <exception cref="ArgumentNullException">Thrown if key is null or empty.</exception>
    public NotHasFilter(string key)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
    }

    /// <summary>
    /// Evaluates whether the feature does not have the specified attribute.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature does not have the specified attribute; otherwise, false.</returns>
    public override bool Evaluate(IFeature feature)
    {
        return feature != null && !feature.Attributes.Exists(Key);
    }
}
