using NetTopologySuite.Features;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Filter that checks if a feature's identifier does NOT equal the specified value.
/// </summary>
public class IdentifierNotEqualsFilter : Filter
{
    /// <summary>
    /// The identifier value to not match.
    /// </summary>
    public string Identifier { get; }

    /// <summary>
    /// Initializes a new identifier not equals filter.
    /// </summary>
    /// <param name="identifier">The identifier value to exclude.</param>
    /// <exception cref="ArgumentNullException">Thrown if identifier is null.</exception>
    public IdentifierNotEqualsFilter(string identifier)
    {
        Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
    }

    /// <summary>
    /// Evaluates whether the feature's identifier does not match.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature has an identifier that does not equal the specified value; otherwise, false.</returns>
    public override bool Evaluate(IFeature feature)
    {
        if (feature == null)
            return false;

        var idObj = feature.Attributes["id"];
        if (idObj == null)
            return false;

        var idString = idObj as string;
        return idString != null && idString != Identifier;
    }
}
