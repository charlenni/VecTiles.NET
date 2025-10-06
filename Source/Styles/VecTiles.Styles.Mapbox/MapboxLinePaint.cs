using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox;

public static class MapboxLinePaint
{
    public static MapboxPaint CreatePaint(ILayerStyle style, Func<string, MapboxSprite> spriteFactory)
    {
        if (style is not MapboxLayerStyle mapboxStyle)
        {
            throw new ArgumentException($"Style {style.GetType()} isn't a MapboxLayerStyle");
        }

        var layout = mapboxStyle.Layout;
        var paint = mapboxStyle.Paint;

        var brush = new MapboxPaint(mapboxStyle.Name);

        // Set defaults
        brush.SetFixColor(new Color(0, 0, 0, 255));
        brush.SetFixStyle(PaintStyle.Stroke);
        brush.SetFixStrokeWidth(1);
        brush.SetFixStrokeCap(StrokeCap.Butt);
        brush.SetFixStrokeJoin(StrokeJoin.Miter);

        // line-cap
        //   Optional enum. One of butt, round, square. Defaults to butt. Interval.
        //   The display of line endings.
        switch (layout.LineCap)
        {
            case "butt":
                brush.SetFixStrokeCap(StrokeCap.Butt);
                break;
            case "round":
                brush.SetFixStrokeCap(StrokeCap.Round);
                break;
            case "square":
                brush.SetFixStrokeCap(StrokeCap.Square);
                break;
            default:
                brush.SetFixStrokeCap(StrokeCap.Butt);
                break;
        }

        // line-join
        //   Optional enum. One of bevel, round, miter. Defaults to miter.
        //   The display of lines when joining.
        switch (layout.LineJoin)
        {
            case "bevel":
                brush.SetFixStrokeJoin(StrokeJoin.Bevel);
                break;
            case "round":
                brush.SetFixStrokeJoin(StrokeJoin.Round);
                break;
            case "mitter":
                brush.SetFixStrokeJoin(StrokeJoin.Miter);
                break;
            default:
                brush.SetFixStrokeJoin(StrokeJoin.Miter);
                break;
        }

        // line-color
        //   Optional color. Defaults to #000000. Disabled by line-pattern. Exponential.
        //   The color with which the line will be drawn.
        if (paint.LineColor.IsConstant)
        {
            brush.SetFixColor(paint.LineColor.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableColor((context) => paint.LineColor.Evaluate(context));
        }

        // line-width
        //   Optional number.Units in pixels.Defaults to 1. Exponential.
        //   Stroke thickness.
        if (paint.LineWidth.IsConstant)
        {
            brush.SetFixStrokeWidth(paint.LineWidth.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableStrokeWidth((context) => paint.LineWidth.Evaluate(context));
        }

        // line-opacity
        //   Optional number. Defaults to 1. Exponential.
        //   The opacity at which the line will be drawn.
        if (paint.LineOpacity.IsConstant)
        {
            brush.SetFixOpacity(paint.LineOpacity.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableOpacity((context) => paint.LineOpacity.Evaluate(context));
        }

        // line-dasharray
        //   Optional array. Units in line widths. Disabled by line-pattern. Interval.
        //   Specifies the lengths of the alternating dashes and gaps that form the dash pattern. 
        //   The lengths are later scaled by the line width.To convert a dash length to pixels, 
        //   multiply the length by the current line width.
        if (paint.LineDashArray.IsConstant)
        {
            if (!paint.LineDashArray.HasOnlyDefault)
            {
                brush.SetFixDashArray(paint.LineDashArray.Evaluate(EvaluationContext.Empty));
            }
        }
        else
        {
            brush.SetVariableDashArray((context) => paint.LineDashArray.Evaluate(context));
        }

        // line-miter-limit
        //   Optional number. Defaults to 2. Requires line-join = miter. Exponential.
        //   Used to automatically convert miter joins to bevel joins for sharp angles.

        // line-round-limit
        //   Optional number. Defaults to 1.05. Requires line-join = round. Exponential.
        //   Used to automatically convert round joins to miter joins for shallow angles.

        // line-translate
        //   Optional array. Units in pixels.Defaults to 0,0. Exponential.
        //   The geometry's offset. Values are [x, y] where negatives indicate left and up, 
        //   respectively.

        // line-translate-anchor
        //   Optional enum. One of map, viewport.Defaults to map. Requires line-translate. Interval.
        //   Control whether the translation is relative to the map (north) or viewport (screen)

        // line-gap-width
        //   Optional number.Units in pixels.Defaults to 0. Exponential.
        //   Draws a line casing outside of a line's actual path.Value indicates the width of 
        //   the inner gap.

        // line-offset
        //   Optional number. Units in pixels. Defaults to 0. Exponential.
        //   The line's offset perpendicular to its direction. Values may be positive or negative, 
        //   where positive indicates "rightwards" (if you were moving in the direction of the line) 
        //   and negative indicates "leftwards".

        // line-blur
        //   Optional number. Units in pixels.Defaults to 0. Exponential.
        //   Blur applied to the line, in pixels.

        // line-pattern
        //   Optional string. Interval.
        //   Name of image in sprite to use for drawing image lines. For seamless patterns, image 
        //   width must be a factor of two (2, 4, 8, …, 512).

        return brush;
    }
}
