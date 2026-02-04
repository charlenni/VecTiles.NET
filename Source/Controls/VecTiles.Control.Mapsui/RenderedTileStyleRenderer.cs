using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Logging;
using Mapsui.Manipulations;
using Mapsui.Rendering;
using Mapsui.Rendering.Skia.SkiaStyles;
using Mapsui.Styles;
using SkiaSharp;

namespace VecTiles.Controls.Mapsui;

public class RenderedTileStyleRenderer : ISkiaStyleRenderer
{
    private readonly SKRect _tileRect = new SKRect(0, 0, 512, 512);

    public bool Draw(SKCanvas canvas, Viewport viewport, ILayer layer, IFeature feature, IStyle style, RenderService renderService, long iteration)
    {
        try
        {
            var renderedTileFeature = feature as RenderedTileFeature;
            var renderedTile = renderedTileFeature?.RenderedTile;

            if (renderedTile == null)
                return false;

            var opacity = (float)(layer.Opacity * style.Opacity);

            if (style is not RenderedTileStyle renderedTileStyle)
                throw new ArgumentException("Excepted a RenderedTileStyle in the RenderedTileStyleRenderer");

            var extent = feature.Extent;

            if (extent == null)
                return false;

            canvas.Save();

            var scale = CreateMatrix(canvas, viewport, extent);
            var context = new Common.Primitives.EvaluationContext(ResolutionToZoomLevel(viewport.Resolution), 1f / scale, (float)viewport.Rotation);

            canvas.ClipRect(_tileRect);

            foreach (var pair in renderedTile.RenderedLayers)
            {
                pair.Value.Draw(canvas, context);
            }

            canvas.Restore();
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, ex.Message, ex);
        }

        return true;
    }

    private float CreateMatrix(SKCanvas canvas, Viewport viewport, MRect extent)
    {
        float scale;

        if (viewport.IsRotated() && viewport.Rotation != 0.0)
        {
            var destinationTopLeft = viewport.WorldToScreen(extent.Left, extent.Top);
            var destinationTopRight = viewport.WorldToScreen(extent.Right, extent.Top);

            var dx = destinationTopRight.X - destinationTopLeft.X;
            var dy = destinationTopRight.Y - destinationTopLeft.Y;

            scale = (float)Math.Sqrt(dx * dx + dy * dy) / 512f;

            canvas.Translate((float)destinationTopLeft.X, (float)destinationTopLeft.Y);
            if (viewport.IsRotated())
                canvas.RotateDegrees((float)viewport.Rotation);
            canvas.Scale(scale);
        }
        else
        {
            var destination = viewport.WorldToScreen(extent);

            var scaleX = (float)(destination.Width / 512f);
            var scaleY = (float)(destination.Height / 512f);

            scale = (float)((scaleX + scaleY) / 2.0);

            canvas.Translate((float)destination.Left, (float)destination.Bottom);
            if (viewport.IsRotated())
                canvas.RotateDegrees((float)viewport.Rotation);
            canvas.Scale(scaleX, scaleY);
        }

        return scale;
    }

    private static MPoint RoundToPixel(ScreenPosition point)
    {
        return new MPoint(
            (float)Math.Round(point.X),
            (float)Math.Round(point.Y));
    }

    private static SKRect RoundToPixel(MRect boundingBox)
    {
        return new SKRect(
            (float)Math.Round(boundingBox.Left),
            (float)Math.Round(Math.Min(boundingBox.Top, boundingBox.Bottom)),
            (float)Math.Round(boundingBox.Right),
            (float)Math.Round(Math.Max(boundingBox.Top, boundingBox.Bottom)));
    }

    private static float ResolutionToZoomLevel(double resolution)
    {
        double initialResolution = 78271.51696402031; // With 512px tiles
        return (float)Math.Log(initialResolution / resolution, 2);
    }
}
