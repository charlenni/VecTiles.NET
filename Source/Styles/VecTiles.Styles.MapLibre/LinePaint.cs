using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.MapLibre.Extensions;

namespace VecTiles.Styles.MapLibre;

/// <summary>
/// Creates a <see cref="Paint"/> for a line layer from the paint
/// properties of a JSON style.
/// </summary>
public static class LinePaint
{
    /// <summary>
    /// Creates a stroke paint from the line properties (color, width, opacity,
    /// dash array, pattern, cap, and join) of the given layer style.
    /// </summary>
    /// <param name="style">The layer style; must be an <see cref="LayerStyle"/>.</param>
    /// <param name="spriteFactory">Factory that resolves a sprite name into a sprite image.</param>
    /// <returns>The paint for the line layer.</returns>
    /// <exception cref="ArgumentException">Thrown if the style is not an <see cref="LayerStyle"/>.</exception>
    public static Paint CreatePaint(ILayerStyle style, Func<string, Sprite> spriteFactory)
    {
        if (style is not LayerStyle omtStyle)
        {
            throw new ArgumentException($"Style {style.GetType()} isn't a LayerStyle");
        }

        var layout = omtStyle.Layout;
        var paint = omtStyle.Paint;

        var brush = new Paint(omtStyle.Name);

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

        // line-miter-limit
        //   Optional number. Defaults to 2. Requires line-join = miter. Exponential.
        //   Used to automatically convert miter joins to bevel joins for sharp angles.
        //   Expressed as the ratio miter length / half stroke width: the join falls
        //   back to Bevel when the corner is sharper than this limit allows.
        if (layout.LineMiterLimit.IsConstant)
        {
            brush.SetFixStrokeMiter(layout.LineMiterLimit.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableStrokeMiter((context) => layout.LineMiterLimit.Evaluate(context));
        }

        // line-round-limit
        //   Optional number. Defaults to 1.05. Requires line-join = round. Exponential.
        //   Used to automatically convert round joins to miter joins for shallow
        //   angles: the miter tip at a shallow corner extends the outer edge by
        //   only miterRatio * half stroke width; when that ratio stays below the
        //   round limit the (cheaper) miter join is visually identical to the
        //   round join and is used instead.
        if (layout.LineRoundLimit.IsConstant)
        {
            brush.SetFixStrokeRoundLimit(layout.LineRoundLimit.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableStrokeRoundLimit((context) => layout.LineRoundLimit.Evaluate(context));
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
        //   The lengths are later scaled by the line width. To convert a dash length to pixels,
        //   multiply the length by the current line width. The renderer expects tile-space px,
        //   so the conversion happens here: like the dash period before, the widths are frozen
        //   at the nearest lower integer zoom level -> the pattern stays glued to the geometry
        //   and snaps discretely at a level change (nothing crawls during zoom animations).
        if (paint.LineDashArray.IsConstant)
        {
            if (!paint.LineDashArray.HasOnlyDefault)
            {
                var values = paint.LineDashArray.Evaluate(EvaluationContext.Empty);
                var lineWidth = paint.LineWidth;

                // The line-width may interpolate with the zoom: pre-scaling
                // the fixed dash values with one frozen width (e.g. the
                // zoom 0 width) would scale the pattern with the wrong
                // factor, so the freeze happens per context at the integer
                // zoom. Like the stroke width (Paint.Update), the widths
                // multiply with the context scale, so the dash pattern and
                // the stroke always agree
                brush.SetVariableDashArray((context) =>
                {
                    var width = lineWidth.IsConstant
                        ? lineWidth.Evaluate(EvaluationContext.Empty)
                        : lineWidth.Evaluate(new EvaluationContext((int)context.Zoom!));
                    var scaled = new float[values.Length];
                    for (var i = 0; i < values.Length; i++)
                        scaled[i] = values[i] * width * context.Scale;
                    return scaled;
                });
            }
        }
        else
        {
            brush.SetVariableDashArray((context) =>
            {
                var values = paint.LineDashArray.Evaluate(context);
                var width = paint.LineWidth.Evaluate(
                    new EvaluationContext((int)context.Zoom!)) * context.Scale;
                var scaled = new float[values.Length];
                for (var i = 0; i < values.Length; i++)
                    scaled[i] = values[i] * width;
                return scaled;
            });
        }

        // line-pattern
        //   Optional string. Interval.
        //   Name of image in sprite to use for drawing image lines. For seamless patterns, image
        //   width must be a factor of two (2, 4, 8, …, 512). A pattern disables line-dasharray
        //   and line-color.
        if (paint.LinePattern.IsConstant)
        {
            var sprite = paint.LinePattern.Evaluate(EvaluationContext.Empty);

            if (!string.IsNullOrEmpty(sprite))
            {
                if (sprite.Contains("{"))
                {
                    brush.SetVariablePattern((context) =>
                    {
                        var name = omtStyle.Paint.LinePattern.Evaluate(context).ReplaceFields(context.Attributes);

                        return spriteFactory(name);
                    });
                }
                else if (!paint.LinePattern.HasOnlyDefault)
                {
                    brush.SetFixPattern(spriteFactory(sprite));
                }
            }
        }
        else
        {
            brush.SetVariablePattern((context) =>
            {
                var name = omtStyle.Paint.LinePattern!.Evaluate(context).ReplaceFields(context.Attributes);

                return spriteFactory(name);
            });
        }

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
