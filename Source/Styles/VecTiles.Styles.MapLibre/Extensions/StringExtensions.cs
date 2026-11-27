using System.Globalization;
using System.Text.RegularExpressions;
using NetTopologySuite.Features;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre.Extensions
{
    /// <summary>
    /// Extension methods for strings used in style properties: replacing
    /// field placeholders ("{tag}") with feature or context values and
    /// applying text transforms.
    /// </summary>
    public static class StringExtensions
    {
        static Regex _regExFields = new Regex(@"\{(.*?)\}", (RegexOptions)8);

        /// <summary>
        /// Replace all fields in string with values
        /// </summary>
        /// <param name="text">String with fields to replace</param>
        /// <param name="attributes">Tags to replace fields with</param>
        /// <returns></returns>
        public static string ReplaceFields(this string text, AttributesTable attributes)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var result = text;

            var match = _regExFields.Match(text);

            while (match.Success)
            {
                var field = match.Groups[1].Captures[0].Value;

                // Search field
                var replacement = string.Empty;

                if (attributes.Exists(field))
                    replacement = attributes[field].ToString();

                // Replace field with new value
                result = result.Replace(match.Groups[0].Captures[0].Value, replacement);

                // Check for next field
                match = match.NextMatch();
            }
            ;

            return result;
        }

        static Regex _regExAttributes = new Regex(@"\{(.*?)\}", RegexOptions.Compiled);

        /// <summary>
        /// Replaces all "{placeholder}" occurrences with feature or context values.
        /// Names starting with "name" fall back to a localized name for the current culture.
        /// Placeholders without a matching value are removed.
        /// </summary>
        /// <param name="text">The string containing placeholders to replace.</param>
        /// <param name="context">The evaluation context providing feature and style attribute lookups; may be null.</param>
        /// <returns>The string with all placeholders resolved.</returns>
        public static string ReplaceWithTags(this string text, EvaluationContext? context = null)
        {
            var match = _regExAttributes.Match(text);

            if (!match.Success)
                return text;

            var result = text;

            while (match.Success)
            {
                var val = match.Groups[1].Value;

                string? replacement = null;

                if (string.IsNullOrEmpty(val))
                    replacement = string.Empty;
                else if (context?.Feature?.Attributes?.Exists(val) ?? false)
                    replacement = context.Feature.Attributes[val].ToString();
                else if (context?.Attributes?.Exists(val) ?? false)
                    replacement = context.Attributes[val].ToString();
                else if (val.StartsWith("name", StringComparison.InvariantCulture) && context?.Feature?.Attributes != null)
                {
                    // Try to take the localized name
                    var code = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

                    if (context.Feature.Attributes.Exists("name:" + code))
                        replacement = context.Feature.Attributes["name:" + code].ToString();
                    else if (context.Feature.Attributes.Exists("name_" + code))
                        replacement = context.Feature.Attributes["name_" + code].ToString();
                    else
                        // We didn't find a name in the tags, so remove this part
                        replacement = string.Empty;
                }

                if (replacement != null)
                    result = result.Replace(match.Groups[0].Captures[0].Value, replacement);

                match = match.NextMatch();
            }

            return result;
        }

        /// <summary>
        /// Applies the given text transform to the string, converting it to upper
        /// or lower case using the current culture.
        /// </summary>
        /// <param name="text">The string to transform.</param>
        /// <param name="textTransform">The transform to apply; "none" returns the string unchanged.</param>
        /// <returns>The transformed string.</returns>
        public static string ReplaceWithTransforms(this string text, TextTransform textTransform)
        {
            return textTransform switch
            {
                TextTransform.Uppercase => text.ToUpper(CultureInfo.CurrentCulture),
                TextTransform.Lowercase => text.ToLower(CultureInfo.CurrentCulture),
                _ => text
            };
        }
    }
}
