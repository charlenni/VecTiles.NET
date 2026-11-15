using System.Globalization;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;

namespace VecTiles.Common.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Converts a string to a <see cref="TextJustify"/> value.
        /// Returns <see cref="TextJustify.Left"/> for "left", <see cref="TextJustify.Right"/> for "right",
        /// <see cref="TextJustify.Center"/> for "center", and <see cref="TextJustify.Auto"/> for any other value.
        /// </summary>
        /// <param name="text">The string to convert.</param>
        /// <returns>The corresponding <see cref="TextJustify"/> value.</returns>
        public static TextJustify ToTextJustify(this string text)
        {
            return text switch
            {
                "left" => TextJustify.Left,
                "right" => TextJustify.Right,
                "center" => TextJustify.Center,
                _ => TextJustify.Auto
            };
        }

        /// <summary>
        /// Converts a string in Mapbox GL format to a Color
        /// 
        /// This function assumes, that alpha is a float in range from 0.0 to 1.0.
        /// It converts this float the range of 0..255 without rounding.
        /// The following colors could be converted:
        /// - Named colors with known Html names 
        /// - Colors as Html color values with leading '#' and 6 or 3 numbers
        /// - Function rgb(r,g,b) with values for red, green and blue
        /// - Function rgba(r,g,b,a) with values for red, green, blue and alpha. Here alpha is between 0.0 and 1.0 like opacity.
        /// - Function hsl(h,s,l) with values hue (0.0 to 360.0), saturation (0.0% - 100.0%) and lightness (0.0% - 100.0%)
        /// - Function hsla(h,s,l,a) with values hue (0.0 to 360.0), saturation (0.0% - 100.0%), lightness (0.0% - 100.0%) and alpha. Here alpha is between 0.0 and 1.0 like opacity.
        /// </summary>
        /// <param name="from">String with HTML color representation or function like rgb() or hsl()</param>
        /// <returns>Converted Skia SKColor</returns>
        public static Color FromString(this string from)
        {
            Color result = Colors.Empty;

            from = from.Trim().ToLower(CultureInfo.CurrentCulture);

            // Check, if it is a known color
            if (KnownColors.ContainsKey(from))
                from = KnownColors[from];

            if (from.StartsWith("#", StringComparison.InvariantCulture))
            {
                if (from.Length == 7)
                {
                    var color = int.Parse(from.Substring(1), NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture);
                    result = new Color((byte)(color >> 16 & 0xFF), (byte)(color >> 8 & 0xFF), (byte)(color & 0xFF), 255);
                }
                else if (from.Length == 4)
                {
                    var color = int.Parse(from.Substring(1), NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture);
                    var r = (byte)((color >> 8 & 0xF) * 16 + (color >> 8 & 0xF));
                    var g = (byte)((color >> 4 & 0xF) * 16 + (color >> 4 & 0xF));
                    var b = (byte)((color & 0xF) * 16 + (color & 0xF));
                    result = new Color(r, g, b, 255);
                }
            }
            else if (from.StartsWith("rgba", StringComparison.InvariantCulture))
            {
                var split = from.Substring(from.IndexOf('(') + 1).TrimEnd(')').Split(',');

                if (split.Length != 4)
                    throw new ArgumentException($"color {from} isn't a valid color");

                var r = byte.Parse(split[0].Trim(), CultureInfo.InvariantCulture);
                var g = byte.Parse(split[1].Trim(), CultureInfo.InvariantCulture);
                var b = byte.Parse(split[2].Trim(), CultureInfo.InvariantCulture);
                var a = float.Parse(split[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);

                result = new Color(r, g, b, (byte)(a * 255));
            }
            else if (from.StartsWith("rgb", StringComparison.InvariantCulture))
            {
                var split = from.Substring(from.IndexOf('(') + 1).TrimEnd(')').Split(',');

                if (split.Length != 3)
                    throw new ArgumentException($"color {from} isn't a valid color");

                var r = byte.Parse(split[0].Trim(), CultureInfo.InvariantCulture);
                var g = byte.Parse(split[1].Trim(), CultureInfo.InvariantCulture);
                var b = byte.Parse(split[2].Trim(), CultureInfo.InvariantCulture);

                result = new Color(r, g, b, 255);
            }
            else if (from.StartsWith("hsla", StringComparison.InvariantCulture))
            {
                var split = from.Substring(from.IndexOf('(') + 1).TrimEnd(')').Split(',');

                if (split.Length != 4)
                    throw new ArgumentException($"color {from} isn't a valid color");

                var h = float.Parse(split[0].Trim(), CultureInfo.InvariantCulture);
                var s = float.Parse(split[1].Trim().Replace("%", ""), CultureInfo.InvariantCulture);
                var l = float.Parse(split[2].Trim().Replace("%", ""), CultureInfo.InvariantCulture);
                var a = float.Parse(split[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);

                result = FromHsl(h / 360.0f, s / 100.0f, l / 100.0f, (int)(a * 255));
            }
            else if (from.StartsWith("hsl", StringComparison.InvariantCulture))
            {
                var split = from.Substring(from.IndexOf('(') + 1).TrimEnd(')').Split(',');

                if (split.Length != 3)
                    throw new ArgumentException($"color {from} isn't a valid color");

                var h = float.Parse(split[0].Trim(), CultureInfo.InvariantCulture);
                var s = float.Parse(split[1].Trim().Replace("%", ""), CultureInfo.InvariantCulture);
                var l = float.Parse(split[2].Trim().Replace("%", ""), CultureInfo.InvariantCulture);

                result = FromHsl(h / 360.0f, s / 100.0f, l / 100.0f);
            }

            return result;
        }

        /// <summary>
        /// Found at http://james-ramsden.com/convert-from-hsl-to-rgb-colour-codes-in-c/
        /// </summary>
        /// <param name="h"></param>
        /// <param name="s"></param>
        /// <param name="l"></param>
        /// <param name="a"></param>
        /// <returns></returns>
        public static Color FromHsl(float h, float s, float l, int a = 255)
        {
            double r = 0, g = 0, b = 0;
            // != 0
            if (l > float.Epsilon)
            {
                // == 0
                if (s < float.Epsilon)
                    r = g = b = l;
                else
                {
                    float temp2;

                    if (l < 0.5)
                        temp2 = l * (1.0f + s);
                    else
                        temp2 = l + s - (l * s);

                    float temp1 = 2.0f * l - temp2;

                    r = GetColorComponent(temp1, temp2, h + 1.0f / 3.0f);
                    g = GetColorComponent(temp1, temp2, h);
                    b = GetColorComponent(temp1, temp2, h - 1.0f / 3.0f);
                }
            }
            return new Color(
                (byte)Math.Round(r * 255.0f),
                (byte)Math.Round(g * 255.0f),
                (byte)Math.Round(b * 255.0f),
                (byte)a);

        }

        /// <summary>
        /// Helper function for FromHsl function
        /// </summary>
        /// <param name="temp1"></param>
        /// <param name="temp2"></param>
        /// <param name="temp3"></param>
        /// <returns></returns>
        private static double GetColorComponent(float temp1, float temp2, float temp3)
        {
            if (temp3 < 0.0f)
                temp3 += 1.0f;
            else if (temp3 > 1.0f)
                temp3 -= 1.0f;

            if (temp3 < 1.0f / 6.0f)
                return temp1 + (temp2 - temp1) * 6.0f * temp3;
            else if (temp3 < 0.5f)
                return temp2;
            else if (temp3 < 2.0f / 3.0f)
                return temp1 + ((temp2 - temp1) * ((2.0f / 3.0f) - temp3) * 6.0f);
            else
                return temp1;
        }

        /// <summary>
        /// Change alpha channel from given color to respect opacity
        /// </summary>
        /// <param name="color">Mapsui Color to change</param>
        /// <param name="opacity">Opacity of the new color</param>
        /// <returns>New color respecting old alpha and new opacity</returns>
        public static Color Opacity(Color color, float? opacity)
        {
            if (opacity == null)
                return color;

            return new Color(color.R, color.G, color.B, (byte)Math.Round(color.A * (float)opacity));
        }

        /// <summary>
        /// Known HTML color names and hex code for RGB color
        /// </summary>
        public static readonly Dictionary<string, string> KnownColors = new Dictionary<string, string>
        {
            {"aliceblue", "#F0F8FF"},
            {"antiquewhite", "#FAEBD7"},
            {"aqua", "#00FFFF"},
            {"aquamarine", "#7FFFD4"},
            {"azure", "#F0FFFF"},
            {"beige", "#F5F5DC"},
            {"bisque", "#FFE4C4"},
            {"black", "#000000"},
            {"blanchedalmond", "#FFEBCD"},
            {"blue", "#0000FF"},
            {"blueviolet", "#8A2BE2"},
            {"brown", "#A52A2A"},
            {"burlywood", "#DEB887"},
            {"cadetblue", "#5F9EA0"},
            {"chartreuse", "#7FFF00"},
            {"chocolate", "#D2691E"},
            {"coral", "#FF7F50"},
            {"cornflowerblue", "#6495ED"},
            {"cornsilk", "#FFF8DC"},
            {"crimson", "#DC143C"},
            {"cyan", "#00FFFF"},
            {"darkblue", "#00008B"},
            {"darkcyan", "#008B8B"},
            {"darkgoldenrod", "#B8860B"},
            {"darkgray", "#A9A9A9"},
            {"darkgrey", "#A9A9A9"},
            {"darkgreen", "#006400"},
            {"darkkhaki", "#BDB76B"},
            {"darkmagenta", "#8B008B"},
            {"darkolivegreen", "#556B2F"},
            {"darkorange", "#FF8C00"},
            {"darkorchid", "#9932CC"},
            {"darkred", "#8B0000"},
            {"darksalmon", "#E9967A"},
            {"darkseagreen", "#8FBC8F"},
            {"darkslateblue", "#483D8B"},
            {"darkslategray", "#2F4F4F"},
            {"darkslategrey", "#2F4F4F"},
            {"darkturquoise", "#00CED1"},
            {"darkviolet", "#9400D3"},
            {"deeppink", "#FF1493"},
            {"deepskyblue", "#00BFFF"},
            {"dimgray", "#696969"},
            {"dimgrey", "#696969"},
            {"dodgerblue", "#1E90FF"},
            {"firebrick", "#B22222"},
            {"floralwhite", "#FFFAF0"},
            {"forestgreen", "#228B22"},
            {"fuchsia", "#FF00FF"},
            {"gainsboro", "#DCDCDC"},
            {"ghostwhite", "#F8F8FF"},
            {"gold", "#FFD700"},
            {"goldenrod", "#DAA520"},
            {"gray", "#808080"},
            {"grey", "#808080"},
            {"green", "#008000"},
            {"greenyellow", "#ADFF2F"},
            {"honeydew", "#F0FFF0"},
            {"hotpink", "#FF69B4"},
            {"indianred", "#CD5C5C"},
            {"indigo", "#4B0082"},
            {"ivory", "#FFFFF0"},
            {"khaki", "#F0E68C"},
            {"lavender", "#E6E6FA"},
            {"lavenderblush", "#FFF0F5"},
            {"lawngreen", "#7CFC00"},
            {"lemonchiffon", "#FFFACD"},
            {"lightblue", "#ADD8E6"},
            {"lightcoral", "#F08080"},
            {"lightcyan", "#E0FFFF"},
            {"lightgoldenrodyellow", "#FAFAD2"},
            {"lightgray", "#D3D3D3"},
            {"lightgrey", "#D3D3D3"},
            {"lightgreen", "#90EE90"},
            {"lightpink", "#FFB6C1"},
            {"lightsalmon", "#FFA07A"},
            {"lightseagreen", "#20B2AA"},
            {"lightskyblue", "#87CEFA"},
            {"lightslategray", "#778899"},
            {"lightslategrey", "#778899"},
            {"lightsteelblue", "#B0C4DE"},
            {"lightyellow", "#FFFFE0"},
            {"lime", "#00FF00"},
            {"limegreen", "#32CD32"},
            {"linen", "#FAF0E6"},
            {"magenta", "#FF00FF"},
            {"maroon", "#800000"},
            {"mediumaquamarine", "#66CDAA"},
            {"mediumblue", "#0000CD"},
            {"mediumorchid", "#BA55D3"},
            {"mediumpurple", "#9370DB"},
            {"mediumseagreen", "#3CB371"},
            {"mediumslateblue", "#7B68EE"},
            {"mediumspringgreen", "#00FA9A"},
            {"mediumturquoise", "#48D1CC"},
            {"mediumvioletred", "#C71585"},
            {"midnightblue", "#191970"},
            {"mintcream", "#F5FFFA"},
            {"mistyrose", "#FFE4E1"},
            {"moccasin", "#FFE4B5"},
            {"navajowhite", "#FFDEAD"},
            {"navy", "#000080"},
            {"oldlace", "#FDF5E6"},
            {"olive", "#808000"},
            {"olivedrab", "#6B8E23"},
            {"orange", "#FFA500"},
            {"orangered", "#FF4500"},
            {"orchid", "#DA70D6"},
            {"palegoldenrod", "#EEE8AA"},
            {"palegreen", "#98FB98"},
            {"paleturquoise", "#AFEEEE"},
            {"palevioletred", "#DB7093"},
            {"papayawhip", "#FFEFD5"},
            {"peachpuff", "#FFDAB9"},
            {"peru", "#CD853F"},
            {"pink", "#FFC0CB"},
            {"plum", "#DDA0DD"},
            {"powderblue", "#B0E0E6"},
            {"purple", "#800080"},
            {"rebeccapurple", "#663399"},
            {"red", "#FF0000"},
            {"rosybrown", "#BC8F8F"},
            {"royalblue", "#4169E1"},
            {"saddlebrown", "#8B4513"},
            {"salmon", "#FA8072"},
            {"sandybrown", "#F4A460"},
            {"seagreen", "#2E8B57"},
            {"seashell", "#FFF5EE"},
            {"sienna", "#A0522D"},
            {"silver", "#C0C0C0"},
            {"skyblue", "#87CEEB"},
            {"slateblue", "#6A5ACD"},
            {"slategray", "#708090"},
            {"slategrey", "#708090"},
            {"snow", "#FFFAFA"},
            {"springgreen", "#00FF7F"},
            {"steelblue", "#4682B4"},
            {"tan", "#D2B48C"},
            {"teal", "#008080"},
            {"thistle", "#D8BFD8"},
            {"tomato", "#FF6347"},
            {"turquoise", "#40E0D0"},
            {"violet", "#EE82EE"},
            {"wheat", "#F5DEB3"},
            {"white", "#FFFFFF"},
            {"whitesmoke", "#F5F5F5"},
            {"yellow", "#FFFF00"},
            {"yellowgreen", "#9ACD32"}
        };
    }
}
