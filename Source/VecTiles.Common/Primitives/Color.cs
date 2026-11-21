namespace VecTiles.Common.Primitives;

/// <summary>
/// Provides a set of predefined default <see cref="Color"/> values.
/// </summary>
public static class  Colors
{
    // Default colors
    /// <summary>
    /// Fully transparent color.
    /// </summary>
    public static Color Empty = new(0, 0, 0, 0);
    /// <summary>
    /// Black color.
    /// </summary>
    public static Color Black = new(0, 0, 0, 255);
    /// <summary>
    /// White color.
    /// </summary>
    public static Color White = new(255, 255, 255, 255);
    /// <summary>
    /// Red color.
    /// </summary>
    public static Color Red = new(255, 0, 0, 255);
    /// <summary>
    /// Green color.
    /// </summary>
    public static Color Green = new(0, 255, 0, 255);
    /// <summary>
    /// Blue color.
    /// </summary>
    public static Color Blue = new(0, 0, 255, 255);
}

/// <summary>
/// Represents an RGBA color with byte components and provides some default colors.
/// </summary>
/// <param name="R">Red part of color between 0 and 255.</param>
/// <param name="G">Green part of color between 0 and 255.</param>
/// <param name="B">Blue part of color between 0 and 255.</param>
/// <param name="A">Alpha part of color between 0 and 255.</param>
public record Color (byte R, byte G, byte B, byte A)
{
    /// <summary>
    /// Returns a string representation of the color in the format: Color [R{R};G{G};B{B};A{A}]
    /// </summary>
    public override string ToString()
    {
        return $"Color [R{R};G{G};B{B};A{A}]";
    }
}
