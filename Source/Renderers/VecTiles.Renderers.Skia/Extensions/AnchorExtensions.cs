using SkiaSharp;
using VecTiles.Common.Enums;

namespace VecTiles.Renderers.Skia.Extensions;

public static class AnchorExtensions
{
    public static SKPoint ToPoint(this Anchor anchor)
    {
        return anchor switch
        {
            Anchor.Center => new SKPoint(-0.5f, -0.5f),
            Anchor.Left => new SKPoint(0, -0.5f),
            Anchor.Right => new SKPoint(-1.0f, -0.5f),
            Anchor.Top => new SKPoint(-0.5f, 0),
            Anchor.Bottom => new SKPoint(-0.5f, -1.0f),
            Anchor.TopLeft => new SKPoint(0, 0),
            Anchor.TopRight => new SKPoint(-1.0f, 0),
            Anchor.BottomLeft => new SKPoint(0, -1.0f),
            Anchor.BottomRight => new SKPoint(-1.0f, -1.0f),
            _ => throw new NotImplementedException($"Unknown Anchor")
        };
    }
}
