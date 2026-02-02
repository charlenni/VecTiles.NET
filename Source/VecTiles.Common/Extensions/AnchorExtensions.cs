using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;

namespace VecTiles.Common.Extensions;

public static class AnchorExtensions
{
    public static Point ToPoint(this Anchor anchor)
    {
        return anchor switch
        {
            Anchor.Center => new Point(-0.5f, -0.5f),
            Anchor.Left => new Point(0, -0.5f),
            Anchor.Right => new Point(-1.0f, -0.5f),
            Anchor.Top => new Point(-0.5f, 0),
            Anchor.Bottom => new Point(-0.5f, -1.0f),
            Anchor.TopLeft => new Point(0, 0),
            Anchor.TopRight => new Point(-1.0f, 0),
            Anchor.BottomLeft => new Point(0, -1.0f),
            Anchor.BottomRight => new Point(-1.0f, -1.0f),
            _ => throw new NotImplementedException($"Unknown Anchor")
        };
    }
}
