namespace VecTiles.Common.Enums;

/// <summary>
/// Specifies the text writing direction used for shaping and layout.
/// </summary>
public enum TextDirection
{
    /// <summary>
    /// Text is laid out from left to right (e.g. Latin scripts).
    /// </summary>
    LeftToRight,
    /// <summary>
    /// Text is laid out from right to left (e.g. Arabic or Hebrew scripts).
    /// </summary>
    RightToLeft,
    /// <summary>
    /// The direction is detected automatically from the text content.
    /// </summary>
    Auto
}