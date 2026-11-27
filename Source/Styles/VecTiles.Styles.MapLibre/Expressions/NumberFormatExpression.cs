using System.Globalization;
using System.Text.Json.Nodes;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre.Expressions;

/// <summary>
/// Implements the "number-format" expression: ["number-format", number, options]
/// like MapLibre ("to-string" with formatting). The options object holds the
/// locale, an optional currency, and the minimum/maximum number of fraction
/// digits.
/// </summary>
public class NumberFormatExpression : Expression
{
    private readonly IExpression _input;

    /// <summary>The locale tag (BCP 47) used for formatting, or null for the invariant culture.</summary>
    public string? Locale { get; }

    /// <summary>The currency code for currency formatting, or null for plain number formatting.</summary>
    public string? Currency { get; }

    /// <summary>The minimum number of fraction digits, or null to use the default.</summary>
    public int? MinFractionDigits { get; }

    /// <summary>The maximum number of fraction digits, or null to use the default.</summary>
    public int? MaxFractionDigits { get; }

    private NumberFormatExpression(IExpression input, string? locale, string? currency, int? minFractionDigits, int? maxFractionDigits)
    {
        _input = input;
        Locale = locale;
        Currency = currency;
        MinFractionDigits = minFractionDigits;
        MaxFractionDigits = maxFractionDigits;
    }

    /// <summary>
    /// Parses a "number-format" expression from the given JSON array.
    /// </summary>
    /// <param name="array">The JSON array of the expression, starting with the operator name.</param>
    /// <param name="parser">The parser used to parse the arguments and report errors.</param>
    /// <returns>The parsed expression, or null if parsing failed.</returns>
    public static IExpression? Parse(JsonArray array, ExpressionParser parser)
    {
        if (array.Count != 3)
        {
            parser.Error("Expected a number and an options object.");
            return null;
        }

        var input = MatchExpression.ParseChild(array[1], 1, parser, typeof(double));

        if (input == null)
            return null;

        if (array[2] is not JsonObject options)
        {
            parser.Error("Expected an options object.");
            return null;
        }

        var locale = options.TryGetPropertyValue("locale", out var localeNode) ? localeNode?.ToString() : null;
        var currency = options.TryGetPropertyValue("currency", out var currencyNode) ? currencyNode?.ToString() : null;

        int? minFractionDigits = options.TryGetPropertyValue("min-fraction-digits", out var minNode) ? (int?)ExpressionValueHelper.AsNumber(MatchExpression.ToValue(minNode)) : null;
        int? maxFractionDigits = options.TryGetPropertyValue("max-fraction-digits", out var maxNode) ? (int?)ExpressionValueHelper.AsNumber(MatchExpression.ToValue(maxNode)) : null;

        return new NumberFormatExpression(input, locale, currency, minFractionDigits, maxFractionDigits);
    }

    /// <summary>
    /// Evaluates the number and formats it like MapLibre: with the locale
    /// settings, as currency when a currency option is given, and with the
    /// configured fraction digit limits.
    /// </summary>
    /// <param name="ctx">The evaluation context.</param>
    /// <returns>The formatted number string, or null if the input is not a number.</returns>
    public override object? Evaluate(EvaluationContext ctx)
    {
        var value = ExpressionValueHelper.AsNumber(_input.Evaluate(ctx));

        if (value == null)
            return null;

        CultureInfo culture;

        try
        {
            culture = string.IsNullOrEmpty(Locale) ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(Locale);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.InvariantCulture;
        }

        return Currency != null ? FormatCurrency(value.Value, culture) : FormatPlain(value.Value, culture);
    }

    private string FormatCurrency(double value, CultureInfo culture)
    {
        var numberFormat = (NumberFormatInfo)culture.NumberFormat.Clone();

        // Symbol after the amount, like the Intl.NumberFormat default used by
        // MapLibre (e.g. "123.456,79 €" instead of "€ 123.456,79")
        numberFormat.CurrencyPositivePattern = 3;
        numberFormat.CurrencyNegativePattern = 8;

        return value.ToString("C", numberFormat);
    }

    private string FormatPlain(double value, CultureInfo culture)
    {
        var numberFormat = culture.NumberFormat;

        var min = Math.Clamp(MinFractionDigits ?? 0, 0, 100);
        var max = Math.Clamp(MaxFractionDigits ?? Math.Max(min, 3), min, 100);

        if (MinFractionDigits == null && MaxFractionDigits == null)
        {
            // MapLibre default: up to 3 fraction digits, like the "N+3" format
            var shortest = value.ToString(culture);

            var decimalSeparatorIndex = shortest.LastIndexOf(numberFormat.NumberDecimalSeparator, StringComparison.Ordinal);

            if (decimalSeparatorIndex < 0)
                return shortest;

            var fractionDigits = shortest.Length - decimalSeparatorIndex - numberFormat.NumberDecimalSeparator.Length;

            return fractionDigits <= max ? shortest : value.ToString("N" + max, numberFormat);
        }

        return value.ToString("N" + max, numberFormat);
    }

    /// <summary>
    /// Always returns null: the possible outputs of a "number-format"
    /// expression cannot be determined statically.
    /// </summary>
    /// <returns>Null, because the possible outputs are unknown.</returns>
    public override object? PossibleOutputs()
    {
        return null;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another
    /// <see cref="NumberFormatExpression"/>, comparing input expression and
    /// all formatting options.
    /// </summary>
    /// <param name="other">The number-format expression to compare with.</param>
    /// <returns>True if the expressions are equal; otherwise, false.</returns>
    public bool Equals(NumberFormatExpression? other)
    {
        return other is not null
            && (ReferenceEquals(this, other)
                || (_input.Equals(other._input)
                    && Locale == other.Locale
                    && Currency == other.Currency
                    && MinFractionDigits == other.MinFractionDigits
                    && MaxFractionDigits == other.MaxFractionDigits));
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is NumberFormatExpression other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return _input.GetHashCode()
                ^ (Locale?.GetHashCode() ?? 0)
                ^ (Currency?.GetHashCode() ?? 0)
                ^ (MinFractionDigits?.GetHashCode() ?? 0)
                ^ (MaxFractionDigits?.GetHashCode() ?? 0);
        }
    }
}
