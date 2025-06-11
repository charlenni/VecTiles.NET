namespace VecTiles.Common.Primitives;

/// <summary>
/// Represents an RGBA color with byte components and provides some default colors.
/// </summary>
/// <param name="R">Red part of color between 0 and 255.</param>
/// <param name="G">Green part of color between 0 and 255.</param>
/// <param name="B">Blue part of color between 0 and 255.</param>
/// <param name="A">Alpha part of color between 0 and 255.</param>
public record Color (byte R, byte G, byte B, byte A)
{
    // Default colors
    public static Color Empty = new(0, 0, 0, 0);
    public static Color Black = new(0, 0, 0, 255);

    /// <summary>
    /// Returns a string representation of the color in the format: Color [R{R};G{G};B{B};A{A}]
    /// </summary>
    public override string ToString()
    {
        return $"Color [R{R};G{G};B{B};A{A}]";
    }
}
