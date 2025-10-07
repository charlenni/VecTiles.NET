using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapbox.Extensions;

namespace VecTiles.Styles.Mapbox;

public static class MapboxFillPaint
{
    public static Paint CreatePaint(ILayerStyle style, Func<string, Sprite> spriteFactory)
    {
        if (style is not MapboxLayerStyle mapboxStyle)
        {
            throw new ArgumentException($"Style {style.GetType()} isn't a MapboxLayerStyle");
        }

        var layout = mapboxStyle.Layout;
        var paint = mapboxStyle.Paint;

        var brush = new Paint(mapboxStyle.Name);

        // Set defaults
        brush.SetFixStyle(paint.FillOutlineColor.HasOnlyDefault ? PaintStyle.Fill : PaintStyle.StrokeAndFill);
        brush.SetFixStrokeWidth(0);

        // fill-color
        //   Optional color. Defaults to #000000. Disabled by fill-pattern. Exponential.
        //   The color of the filled part of this layer. This color can be specified as 
        //   rgba with an alpha component and the color's opacity will not affect the 
        //   opacity of the 1px stroke, if it is used.
        if (paint.FillColor.IsConstant)
        {
            brush.SetFixColor(mapboxStyle.Paint.FillColor.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableColor((context) => mapboxStyle.Paint.FillColor.Evaluate(context));
        }

        // fill-opacity
        //   Optional number. Defaults to 1. Exponential.
        //   The opacity of the entire fill layer. In contrast to the fill-color, this 
        //   value will also affect the 1px stroke around the fill, if the stroke is used.
        if (paint.FillOpacity.IsConstant)
        {
            brush.SetFixOpacity(mapboxStyle.Paint.FillOpacity.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableOpacity((context) => mapboxStyle.Paint.FillOpacity.Evaluate(context));
        }

        // fill-antialias
        //   Optional boolean. Defaults to true. Interval.
        //   Whether or not the fill should be antialiased.
        if (paint.FillAntialias.IsConstant)
        {
            brush.SetFixAntialias(mapboxStyle.Paint.FillAntialias.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableAntialias((context) => mapboxStyle.Paint.FillAntialias.Evaluate(context));
        }

        // fill-outline-color
        //   Optional color. Disabled by fill-pattern. Requires fill-antialias = true. Exponential. 
        //   The outline color of the fill. Matches the value of fill-color if unspecified.
        if (paint.FillOutlineColor.IsConstant)
        {
            brush.SetFixOutlineColor(mapboxStyle.Paint.FillOutlineColor.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableOutlineColor((context) => mapboxStyle.Paint.FillOutlineColor!.Evaluate(context));
        }

        // fill-translate
        //   Optional array. Units in pixels. Defaults to 0,0. Exponential.
        //   The geometry's offset. Values are [x, y] where negatives indicate left and up, 
        //   respectively.

        // TODO: Use matrix of paint object for this

        // fill-translate-anchor
        //   Optional enum. One of map, viewport. Defaults to map. Requires fill-translate. Interval.
        //   Control whether the translation is relative to the map (north) or viewport (screen)

        // TODO: Use matrix of paint object for this

        // fill-pattern
        //   Optional string. Interval.
        //   Name of image in sprite to use for drawing image fills. For seamless patterns, 
        //   image width and height must be a factor of two (2, 4, 8, …, 512).
        if (paint.FillPattern != null)
        {
            // FillPattern needs a color. Instead no pattern is drawn.
            //area.SetFixColor(SKColors.Black);

            if (paint.FillPattern.IsConstant)
            {
                var sprite = paint.FillPattern.Evaluate(EvaluationContext.Empty);

                if (!string.IsNullOrEmpty(sprite))
                {
                    if (sprite.Contains("{"))
                    {
                        brush.SetVariablePattern((context) =>
                        {
                            var name = mapboxStyle.Paint.FillPattern.Evaluate(context).ReplaceFields(context.Attributes);

                            return spriteFactory(name);
                        });
                    }
                    else
                    {
                        brush.SetVariablePattern((context) =>
                        {
                            return spriteFactory(sprite);
                        });
                    }
                }
            }
            else
            {
                brush.SetVariablePattern((context) =>
                {
                    var name = mapboxStyle.Paint.FillPattern!.Evaluate(context).ReplaceFields(context.Attributes);

                    return spriteFactory(name);
                });
            }
        }

        return brush;
    }
}
