using System.Collections.Generic;
using NetTopologySuite.Features;

namespace VecTiles.Styles.OpenMapTiles.Filter;

/// <summary>
/// Filter that checks if a feature's identifier is contained in the specified list of values.
/// Uses a HashSet for O(1) lookup performance when the list has more than 10 elements.
/// </summary>
public class IdentifierInFilter : Filter
{
    /// <summary>
    /// The list of identifier values to match against.
    /// </summary>
    public IList<string> Identifiers { get; }

    /// <summary>
    /// HashSet for O(1) lookups (created lazily when list has more than 10 elements).
    /// </summary>
    private HashSet<string> _identifierSet;

    /// <summary>
    /// Save Count for later use.
    /// </summary>
    private int _identifiersCount = 0;

    /// <summary>
    /// Initializes a new identifier in filter.
    /// </summary>
    /// <param name="identifiers">The collection of identifiers to match.</param>
    /// <exception cref="ArgumentNullException">Thrown if identifiers is null.</exception>
    public IdentifierInFilter(IEnumerable<string> identifiers)
    {
        Identifiers = new List<string>();

        if (identifiers == null)
            return;

        foreach (var identifier in identifiers)
        {
            if (!string.IsNullOrEmpty(identifier))
                Identifiers.Add(identifier);
        }

        _identifiersCount = Identifiers.Count;
        
        // Create HashSet for O(1) lookups if we have more than 10 identifiers
        if (_identifiersCount > 10)
        {
            _identifierSet = new HashSet<string>(Identifiers, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Gets the HashSet for fast lookups, creating it if necessary.
    /// </summary>
    private HashSet<string> IdentifierSet
    {
        get
        {
            if (_identifierSet == null && _identifiersCount > 10)
            {
                _identifierSet = new HashSet<string>(Identifiers, StringComparer.Ordinal);
            }
            return _identifierSet;
        }
    }

    /// <summary>
    /// Evaluates whether the feature's identifier is in the list.
    /// </summary>
    /// <param name="feature">The feature to evaluate.</param>
    /// <returns>True if the feature has an identifier that is in the specified list; otherwise, false.</returns>
    public override bool Evaluate(IFeature feature)
    {
        if (feature == null)
            return false;

        var idObj = feature.Attributes["id"];
        if (idObj == null)
            return false;

        var idString = idObj as string;
        if (string.IsNullOrEmpty(idString))
            return false;

        // Use HashSet for O(1) lookup if we have more than 10 identifiers
        if (_identifiersCount > 10 && _identifierSet != null)
        {
            return _identifierSet.Contains(idString);
        }

        // For small lists, linear search might be faster due to cache locality
        for (var i = 0; i < _identifiersCount; i++)
        {
            if (idString == Identifiers[i])
                return true;
        }

        return false;
    }
}
