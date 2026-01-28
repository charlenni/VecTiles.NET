using NetTopologySuite.Geometries;
using SkiaSharp;

namespace VecTiles.Renderers.Skia.Extensions;

public static class EnvelopeExtensions
{
    public static SKRect ToSKRect(this Envelope envelope)
    {
        return new SKRect((float)envelope.MinX, (float)envelope.MinY, (float)envelope.MaxX, (float)envelope.MaxY);
    }
}