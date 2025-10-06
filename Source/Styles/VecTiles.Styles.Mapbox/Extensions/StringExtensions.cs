using NetTopologySuite.Features;
using System.Globalization;
using System.Text.RegularExpressions;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox.Extensions
{
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

        static Regex _regExAttributes = new Regex(@".*\{(.*)\}.*", RegexOptions.Compiled);

        public static string ReplaceWithTags(this string text, EvaluationContext? context = null)
        {
            var match = _regExAttributes.Match(text);

            if (!match.Success)
                return text;

            var val = match.Groups[1].Value;

            if (string.IsNullOrEmpty(val))
                return text.Replace("{{}}", "");

            if (context?.Feature?.Attributes?.Exists(val) ?? false)
                return text.Replace($"{{{val}}}", context.Feature.Attributes[val].ToString());

            if (context?.Attributes?.Exists(val) ?? false)
                return text.Replace($"{{{val}}}", context.Attributes[val].ToString());

            if (context?.Feature?.Attributes == null)
                return text;

            // Check, if match starts with name
            if (val.StartsWith("name"))
            {
                // Try to take the localized name
                var code = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

                if (context.Feature.Attributes.Exists("name:" + code))
                    return text.Replace($"{{{val}}}", context.Feature.Attributes["name:" + code].ToString());
                if (context.Feature.Attributes.Exists("name_" + code))
                    return text.Replace($"{{{val}}}", context.Feature.Attributes["name_" + code].ToString());

                // We didn't find a name in the tags, so remove this part
                return text.Replace($"{{{val}}}", "");
            }

            return text;
        }

        public static string ReplaceWithTransforms(this string text, TextTransform textTransform)
        {
            return textTransform switch
            {
                TextTransform.Uppercase => text.ToUpper(),
                TextTransform.Lowercase => text.ToLower(),
                _ => text
            };
        }
    }
}
