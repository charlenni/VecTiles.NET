using NetTopologySuite.Geometries;
using SkiaSharp;

namespace VecTiles.Renderers.Skia.Extensions;

/// <summary>
/// Extension methods to convert NTS envelopes to SkiaSharp rectangles.
/// </summary>
public static class EnvelopeExtensions
{
    /// <summary>
    /// Converts the envelope to an <see cref="SKRect" />.
    /// </summary>
    /// <param name="envelope">Envelope to convert.</param>
    /// <returns>The equivalent SkiaSharp rectangle.</returns>
    public static SKRect ToSKRect(this Envelope envelope)
    {
        return new SKRect((float)envelope.MinX, (float)envelope.MinY, (float)envelope.MaxX, (float)envelope.MaxY);
    }
}