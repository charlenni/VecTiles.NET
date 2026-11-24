namespace VecTiles.Styles.Mapsforge.Enums;

/// <summary>
/// Match mode of a tag rule of a Mapsforge render theme. The semantics follow
/// the rule evaluation of the Mapsforge library (RuleBuilder, PositiveRule,
/// NegativeRule).
/// </summary>
public enum MapsforgeMatchMode
{
    /// <summary>Matches when a tag with one of the keys carries one of the
    /// values (wildcard "*" as key and/or value matches arbitrary keys and
    /// values).</summary>
    Positive,
    /// <summary>Non-exclusive negation (tilde "~" in the value list): matches
    /// when none of the keys is present, or when a tag with one of the keys
    /// carries one of the remaining values.</summary>
    Negation,
    /// <summary>Exclusive negation (minus "-" in the value list): matches when
    /// none of the keys is present, or when no tag with one of the keys
    /// carries one of the remaining values.</summary>
    Exclusion
}
