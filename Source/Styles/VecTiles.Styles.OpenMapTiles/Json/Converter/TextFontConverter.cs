using VecTiles.Styles.OpenMapTiles.Json.Converter;

namespace VecTiles.Styles.OpenMapTiles.Json.Converter;

/// <summary>
/// JSON converter for the "text-font" property, which accepts font name lists
/// or expressions evaluating to one (see <see cref="StringArrayConverter"/>).
/// </summary>
public class TextFontConverter : StringArrayConverter
{
}
