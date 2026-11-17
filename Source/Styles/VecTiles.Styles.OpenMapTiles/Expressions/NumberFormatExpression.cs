using System.Globalization;
using Newtonsoft.Json.Linq;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Implements the "number-format" expression: ["number-format", number, options]
/// like MapLibre ("to-string" with formatting). The options object holds the
/// locale, an optional currency, and the minimum/maximum number of fraction
/// digits.
/// </summary>
public class NumberFormatExpression : Expression
{
    private readonly IExpression _input;

    public string? Locale { get; }

    public string? Currency { get; }

    public int? MinFractionDigits { get; }

    public int? MaxFractionDigits { get; }

    private NumberFormatExpression(IExpression input, string? locale, string? currency, int? minFractionDigits, int? maxFractionDigits)
    {
        _input = input;
        Locale = locale;
        Currency = currency;
        MinFractionDigits = minFractionDigits;
        MaxFractionDigits = maxFractionDigits;
    }

    public static IExpression? Parse(JArray array, ExpressionParser parser)
    {
        if (array.Count != 3)
        {
            parser.Error("Expected a number and an options object.");
            return null;
        }

        var input = MatchExpression.ParseChild(array[1], 1, parser, typeof(double));

        if (input == null)
            return null;

        if (array[2] is not JObject options)
        {
            parser.Error("Expected an options object.");
            return null;
        }

        var locale = options.TryGetValue("locale", out var localeToken) ? localeToken.ToString() : null;
        var currency = options.TryGetValue("currency", out var currencyToken) ? currencyToken.ToString() : null;

        int? minFractionDigits = options.TryGetValue("min-fraction-digits", out var minToken) ? (int?)minToken.Value<double>() : null;
        int? maxFractionDigits = options.TryGetValue("max-fraction-digits", out var maxToken) ? (int?)maxToken.Value<double>() : null;

        return new NumberFormatExpression(input, locale, currency, minFractionDigits, maxFractionDigits);
    }

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

    public override object? PossibleOutputs()
    {
        return null;
    }

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

    public override bool Equals(object? obj) => obj is NumberFormatExpression other && Equals(other);

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
