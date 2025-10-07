using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapbox.Extensions;

namespace VecTiles.Styles.Mapbox;

public static class MapboxBackgroundPaint
{
    public static Paint CreatePaint(ILayerStyle style, Func<string, Sprite> spriteFactory)
    {
        var mapboxStyle = (MapboxLayerStyle)style;

        // Background has only properties in Paint, no in Layout
        var paint = mapboxStyle.Paint;

        var brush = new Paint(mapboxStyle.Name);

        brush.SetFixStyle(PaintStyle.Fill);

        // background-color
        //   Optional color. Defaults to #000000. Disabled by background-pattern. Transitionable.
        //   The color with which the background will be drawn.
        if (paint.BackgroundColor.IsConstant)
        {
            brush.SetFixColor(paint.BackgroundColor.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableColor((context) => paint.BackgroundColor.Evaluate(context));
        }

        // background-pattern
        //   Optional string. Interval.
        //   Name of image in sprite to use for drawing image background. For seamless patterns, 
        //   image width and height must be a factor of two (2, 4, 8, …, 512). Note that 
        //   zoom -dependent expressions will be evaluated only at integer zoom levels.
        if (paint.BackgroundPattern.IsConstant && !paint.BackgroundPattern.Evaluate(EvaluationContext.Empty).Contains("{"))
        {
            var sprite = paint.BackgroundPattern.Evaluate(EvaluationContext.Empty);

            if (!string.IsNullOrEmpty(sprite))
            {
                brush.SetFixPattern(spriteFactory(paint.BackgroundPattern.Evaluate(EvaluationContext.Empty)));
            }
        }
        else
        {
            brush.SetVariablePattern((context) =>
            {
                var name = paint.BackgroundPattern.Evaluate(context).ReplaceFields(null);

                return spriteFactory(name);
            });
        }

        // background-opacity
        //   Optional number. Defaults to 1.
        //   The opacity at which the background will be drawn.
        if (paint?.BackgroundOpacity != null)
        {
            if (paint.BackgroundOpacity.IsConstant)
            {
                brush.SetFixOpacity(paint.BackgroundOpacity.Evaluate(EvaluationContext.Empty));
            }
            else
            {
                brush.SetVariableOpacity((context) => paint.BackgroundOpacity.Evaluate(context));
            }
        }

        return brush;
    }
}
