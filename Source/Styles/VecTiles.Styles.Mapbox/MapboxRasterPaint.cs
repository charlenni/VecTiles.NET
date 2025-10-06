using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox;

public static class MapboxRasterPaint
{
    public static MapboxPaint CreatePaint(ILayerStyle style, Func<string, MapboxSprite> spriteFactory)
    {
        if (style is not MapboxLayerStyle mapboxStyle)
        {
            throw new ArgumentException($"Style {style.GetType()} isn't a MapboxLayerStyle");
        }

        // Raster has only properties in Paint, no in Layout
        var paint = mapboxStyle.Paint;

        var brush = new MapboxPaint(mapboxStyle.Name);

        // raster-opacity
        //   Optional number. Defaults to 1.
        //   The opacity at which the image will be drawn.
        if (paint.RasterOpacity.IsConstant)
        {
            brush.SetFixOpacity(paint.RasterOpacity.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableOpacity((context) => paint.RasterOpacity.Evaluate(context));
        }

        // TODO: Create ColorFilter in SKPaint from the following values

        // raster-hue-rotate
        //   Optional number. Units in degrees. Defaults to 0.
        //   Rotates hues around the color wheel.

        // raster-brightness-min
        //   Optional number.Defaults to 0.
        //   Increase or reduce the brightness of the image. The value is the minimum brightness.

        // raster-brightness-max
        //   Optional number. Defaults to 1.
        //   Increase or reduce the brightness of the image. The value is the maximum brightness.

        // raster-saturation
        //   Optional number.Defaults to 0.
        //   Increase or reduce the saturation of the image.

        // raster-contrast
        //   Optional number. Defaults to 0.
        //   Increase or reduce the contrast of the image.

        // raster-fade-duration
        //   Optional number.Units in milliseconds.Defaults to 300.
        //   Fade duration when a new tile is added.

        return brush;
    }
}
