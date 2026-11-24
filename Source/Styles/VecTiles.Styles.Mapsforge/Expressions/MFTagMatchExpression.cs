using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapsforge.Enums;

namespace VecTiles.Styles.Mapsforge.Expressions;

/// <summary>
/// Expression evaluating the key/value match of one Mapsforge render theme rule
/// (attributes "k" and "v") against the raw tags of a feature. A tag match
/// succeeds when a single tag of the feature simultaneously matches the key and
/// the value condition; the negation and exclusion modes follow the semantics
/// of the Mapsforge rule engine.
/// </summary>
public class MFTagMatchExpression : IExpression
{
    /// <summary>
    /// Tag keys the rule matches, already split at "|". An empty list or a
    /// list of "*" matches any key.
    /// </summary>
    private readonly string[] _keys;

    /// <summary>
    /// Tag values the rule matches. In positive mode, "split at |" of the rule value; an empty
    /// list or a list of "*" matches any value. In negation/exclusion mode, the marker token
    /// ("~"/"-") is removed and the remaining values match literally (no wildcard resolution),
    /// following the Mapsforge NegativeMatcher.
    /// </summary>
    private readonly string[] _values;

    /// <summary>
    /// Match mode of the rule (positive, non-exclusive negation, exclusive negation).
    /// </summary>
    private readonly MapsforgeMatchMode _mode;

    /// <summary>
    /// Gets a value indicating whether any key of the feature matches
    /// (<c>k="*"</c>).
    /// </summary>
    private readonly bool _anyKey;

    /// <summary>
    /// Gets a value indicating whether any value of the feature matches
    /// (<c>v="*"</c>, positive mode only).
    /// </summary>
    private readonly bool _anyValue;

    /// <summary>
    /// Initializes a tag match expression.
    /// </summary>
    /// <param name="keys">Tag keys of the rule ("k" attribute, already split at "|").</param>
    /// <param name="values">Tag values of the rule ("v" attribute, already split at "|"; the
    /// "~"/"-" marker token is removed here, in positive mode a "*" is a wildcard, in
    /// negation/exclusion mode the remaining values match literally).</param>
    /// <param name="mode">Match mode derived from the value list markers.</param>
    public MFTagMatchExpression(string[] keys, string[] values, MapsforgeMatchMode mode)
    {
        _keys = keys;
        // The negation/exclusion marker is part of the value list passed by the rule parser;
        // the Mapsforge rule engine removes the marker token before matching (RuleBuilder:
        // valueList.remove("~"/"-")) and matches the remaining values literally, so a "*" in a
        // negated rule is NOT a wildcard (in the themes, v="key=~|*" means "key not present")
        _values = mode == MapsforgeMatchMode.Positive
            ? values
            : values.Where(value => value is not ("~" or "-")).ToArray();
        _mode = mode;
        _anyKey = keys.Length == 0 || keys.Contains("*");
        _anyValue = mode == MapsforgeMatchMode.Positive &&
            (values.Length == 0 || values.Contains("*"));
    }

    /// <inheritdoc />
    /// <returns><c>true</c> when the rule matches the feature tags, otherwise <c>false</c>.</returns>
    public object? Evaluate(EvaluationContext ctx)
    {
        var attributes = GetAttributes(ctx);

        if (attributes == null)
        {
            // Without attributes only the wildcard rule matches
            return _anyKey && _anyValue;
        }

        switch (_mode)
        {
            case MapsforgeMatchMode.Positive:
                return MatchesAnyTag(attributes);

            case MapsforgeMatchMode.Negation:
            case MapsforgeMatchMode.Exclusion:
            {
                // Both negation modes match when none of the keys is present
                if (!ContainsKey(attributes))
                {
                    return true;
                }

                // Tilde: a tag with key and one of the listed values must be present.
                // Minus: no tag with key and one of the listed values may be present.
                // The Mapsforge NegativeMatcher compares keys and values literally, no wildcard
                var tagMatchesValue = attributes.Any(tag =>
                    _keys.Contains(tag.Key) && _values.Contains(tag.Value));

                return _mode == MapsforgeMatchMode.Negation ? tagMatchesValue : !tagMatchesValue;
            }

            default:
                return false;
        }
    }

    /// <inheritdoc />
    public object? PossibleOutputs()
    {
        return new[] { true, false };
    }

    /// <summary>
    /// Gets the raw tags of the evaluation context as key/value pairs, or null
    /// when neither the context nor the feature carries attributes.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, string>>? GetAttributes(EvaluationContext ctx)
    {
        var attributes = ctx.Attributes ?? ctx.Feature?.Attributes;

        if (attributes == null)
        {
            return null;
        }

        return attributes.GetNames()
            .Select(name => new KeyValuePair<string, string>(name, attributes.GetOptionalValue(name)?.ToString() ?? string.Empty));
    }

    /// <summary>
    /// Checks whether the feature carries a tag with one of the keys of the rule.
    /// </summary>
    private bool ContainsKey(IEnumerable<KeyValuePair<string, string>> attributes)
    {
        if (_anyKey)
        {
            return attributes.Any();
        }

        return attributes.Any(tag => _keys.Contains(tag.Key));
    }

    /// <summary>
    /// Checks whether a single tag of the feature matches the key and value
    /// condition of the rule at the same time.
    /// </summary>
    private bool MatchesAnyTag(IEnumerable<KeyValuePair<string, string>> attributes)
    {
        return attributes.Any(tag =>
            (_anyKey || _keys.Contains(tag.Key)) &&
            (_anyValue || _values.Contains(tag.Value)));
    }
}
