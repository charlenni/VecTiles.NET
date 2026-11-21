using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;

namespace VecTiles.Common.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="Anchor"/> enumeration.
/// </summary>
public static class AnchorExtensions
{
    /// <summary>
    /// Converts an <see cref="Anchor"/> to a normalized <see cref="Point"/> with coordinates
    /// in the range 0..1, where the point marks the anchor position within a symbol.
    /// </summary>
    /// <param name="anchor">The anchor to convert.</param>
    /// <returns>The normalized anchor point.</returns>
    public static Point ToPoint(this Anchor anchor)
    {
        return anchor switch
        {
            Anchor.Center => new Point(0.5f, 0.5f),
            Anchor.Left => new Point(0, 0.5f),
            Anchor.Right => new Point(1.0f, 0.5f),
            Anchor.Top => new Point(0.5f, 0),
            Anchor.Bottom => new Point(0.5f, 1.0f),
            Anchor.TopLeft => new Point(0, 0),
            Anchor.TopRight => new Point(1.0f, 0),
            Anchor.BottomLeft => new Point(0, 1.0f),
            Anchor.BottomRight => new Point(1.0f, 1.0f),
            _ => throw new NotImplementedException($"Unknown Anchor")
        };
    }
}
