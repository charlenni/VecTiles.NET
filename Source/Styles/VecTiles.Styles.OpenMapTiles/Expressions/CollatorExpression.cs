using System.Globalization;
using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Collator for locale-aware string comparison, Result of the "collator"
/// expression. Options follow the style spec: case-sensitivity,
/// diacritic-sensitivity and locale.
/// </summary>
internal class Collator
{
    private readonly CompareInfo? _compareInfo;

    private readonly CompareOptions _compareOptions;

    public Collator(bool caseSensitive, bool diacriticSensitive, string? locale, bool ignorePunctuation = false)
    {
        CaseSensitive = caseSensitive;
        DiacriticSensitive = diacriticSensitive;
        Locale = locale;
        IgnorePunctuation = ignorePunctuation;

        _compareOptions = CompareOptions.None
            | (caseSensitive ? CompareOptions.None : CompareOptions.IgnoreCase)
            | (diacriticSensitive ? CompareOptions.None : CompareOptions.IgnoreNonSpace)
            | (ignorePunctuation ? CompareOptions.IgnoreSymbols : CompareOptions.None);

        if (!string.IsNullOrEmpty(locale))
        {
            try
            {
                _compareInfo = new CultureInfo(locale).CompareInfo;
            }
            catch (CultureNotFoundException)
            {
                // Unknown locale: comparisons use ordinal comparison
            }
        }
    }

    public bool CaseSensitive { get; }

    public bool DiacriticSensitive { get; }

    public string? Locale { get; }

    public bool IgnorePunctuation { get; }

    /// <summary>
    /// Compares two strings respecting the collator options. If no locale
    /// comparison is possible (unknown locale or relaxed options) the fallback
    /// is a plain ordinal comparison with respect to case sensitivity.
    /// </summary>
    public int Compare(string left, string right)
    {
        if (_compareInfo != null)
            return _compareInfo.Compare(left, right, _compareOptions);

        if (!CaseSensitive)
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);

        return string.CompareOrdinal(left, right);
    }

    public bool Equals(Collator? other)
    {
        return other is not null
            && CaseSensitive == other.CaseSensitive
            && DiacriticSensitive == other.DiacriticSensitive
            && IgnorePunctuation == other.IgnorePunctuation
            && string.Equals(Locale, other.Locale, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Implements the "collator" expression: ["collator", { "case-sensitive": true,
/// "diacritic-sensitive": true, "locale": "de" }]. The options must be literals.
/// </summary>
public class CollatorExpression : Expression
{
    private readonly Collator _collator;

    private CollatorExpression(Collator collator)
    {
        _collator = collator;
    }

    /// <summary>
    /// Parses a "collator" expression from the given JSON array.
    /// </summary>
    /// <param name="array">The JSON array of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
    {
        if (array.Count != 2 || array[1] is not JsonObject options)
        {
            parser.Error("Expected exactly one object literal argument.");
            return null;
        }

        var caseSensitive = GetOption(options, "case-sensitive");
        var diacriticSensitive = GetOption(options, "diacritic-sensitive");
        var locale = options.TryGetPropertyValue("locale", out var localeNode) ? localeNode?.ToString() : null;
        var ignorePunctuation = GetOption(options, "ignore-punctuation");

        return new CollatorExpression(new Collator(caseSensitive, diacriticSensitive, locale, ignorePunctuation));
    }

    /// <summary>
    /// Reads an optional boolean option. A missing option means false.
    /// </summary>
    private static bool GetOption(JsonObject options, string name)
    {
        return options.TryGetPropertyValue(name, out var node)
            && node is JsonValue value
            && value.TryGetValue<bool>(out var result)
            && result;
    }

    /// <summary>
    /// Evaluates to the collator configured by the parsed options.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>The <see cref="Collator"/> instance of this expression.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        return _collator;
    }

    /// <summary>
    /// Always returns null: the possible outputs of a "collator" expression
    /// cannot be determined statically.
    /// </summary>
    /// <returns>Null, because the possible outputs are unknown.</returns>
    public override object? PossibleOutputs()
    {
        return null;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="CollatorExpression"/>, comparing the underlying collator options.
    /// </summary>
    /// <param name="other">The collator expression to compare with.</param>
    /// <returns>True if the collator expressions are equal; otherwise, false.</returns>
    public bool Equals(CollatorExpression? other) => other is not null && _collator.Equals(other._collator);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is CollatorExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => (_collator.CaseSensitive, _collator.DiacriticSensitive, _collator.IgnorePunctuation, _collator.Locale).GetHashCode();
}
