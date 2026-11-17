using NetTopologySuite.Features;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Filter that checks if a feature's identifier equals the specified value.
/// </summary>
public class IdentifierEqualsFilter : Filter
{
    /// <summary>
    /// The identifier value to match.
    /// </summary>
    public string Identifier { get; }

    /// <summary>
    /// Initializes a new identifier equals filter.
    /// </summary>
    /// <param name="identifier">The identifier value to match.</param>
    /// <exception cref="ArgumentNullException">Thrown if identifier is null.</exception>
    public IdentifierEqualsFilter(string identifier)
    {
        Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
    }

    /// <summary>
    /// Evaluates whether the feature's identifier matches.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature has an identifier that equals the specified value; otherwise, false.</returns>
    public override bool Evaluate(IFeature feature)
    {
        if (feature == null)
            return false;

        var idObj = feature.Attributes["id"];
        if (idObj == null)
            return false;

        var idString = idObj as string;
        return idString != null && idString == Identifier;
    }
}
