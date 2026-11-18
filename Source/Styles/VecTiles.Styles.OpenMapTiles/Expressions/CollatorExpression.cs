using System.Globalization;
using Newtonsoft.Json.Linq;
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

    public static IExpression? Parse(JArray array, ExpressionParser parser)
    {
        if (array.Count != 2 || array[1] is not JObject options)
        {
            parser.Error("Expected exactly one object literal argument.");
            return null;
        }

        var caseSensitive = options.TryGetValue("case-sensitive", out var caseToken) && caseToken.Value<bool>();
        var diacriticSensitive = options.TryGetValue("diacritic-sensitive", out var diacriticToken) && diacriticToken.Value<bool>();
        var locale = options.TryGetValue("locale", out var localeToken) ? localeToken.Value<string>() : null;
        var ignorePunctuation = options.TryGetValue("ignore-punctuation", out var punctuationToken) && punctuationToken.Value<bool>();

        return new CollatorExpression(new Collator(caseSensitive, diacriticSensitive, locale, ignorePunctuation));
    }

    public override object? Evaluate(EvaluationContext ctx)
    {
        return _collator;
    }

    public override object? PossibleOutputs()
    {
        return null;
    }

    public bool Equals(CollatorExpression? other) => other is not null && _collator.Equals(other._collator);

    public override bool Equals(object? obj) => obj is CollatorExpression other && Equals(other);

    public override int GetHashCode() => (_collator.CaseSensitive, _collator.DiacriticSensitive, _collator.IgnorePunctuation, _collator.Locale).GetHashCode();
}
