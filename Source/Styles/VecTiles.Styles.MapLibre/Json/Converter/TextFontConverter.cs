using VecTiles.Styles.MapLibre.Json.Converter;

namespace VecTiles.Styles.MapLibre.Json.Converter;

/// <summary>
/// JSON converter for the "text-font" property, which accepts font name lists
/// or expressions evaluating to one (see <see cref="StringArrayConverter"/>).
/// </summary>
public class TextFontConverter : StringArrayConverter
{
}
