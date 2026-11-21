using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles;

/// <summary>
/// Creates a <see cref="Paint"/> for a circle layer from the paint
/// properties of an OpenMapTiles style.
/// </summary>
public static class OMTCirclePaint
{
    /// <summary>
    /// Creates a fill paint from the circle properties (radius, color, blur, stroke)
    /// of the given layer style.
    /// </summary>
    /// <param name="style">The layer style; must be an <see cref="OMTLayerStyle"/>.</param>
    /// <param name="spriteFactory">Factory that resolves a sprite name into a sprite image.</param>
    /// <returns>The paint for the circle layer.</returns>
    /// <exception cref="ArgumentException">Thrown if the style is not an <see cref="OMTLayerStyle"/>.</exception>
    public static Paint CreatePaint(ILayerStyle style, Func<string, Sprite> spriteFactory)
    {
        if (style is not OMTLayerStyle omtStyle)
        {
            throw new ArgumentException($"Style {style.GetType()} isn't a OMTLayerStyle");
        }

        var layout = omtStyle.Layout;
        var paint = omtStyle.Paint;

        var brush = new Paint(omtStyle.Name);

        // Set defaults
        brush.SetFixStyle(PaintStyle.Fill);
        brush.SetFixStrokeWidth(0);
        brush.SetFixRadius(5);
        brush.SetFixBlur(0);

        // circle-color
        //   Optional color. Defaults to #000000. Interpolated.
        //   The fill color of the circle.
        if (paint.CircleColor.IsConstant)
        {
            brush.SetFixColor(paint.CircleColor.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableColor((context) => paint.CircleColor.Evaluate(context));
        }

        // circle-opacity
        //   Optional number. Defaults to 1. Interpolated.
        //   The opacity at which the circle will be drawn.
        if (paint.CircleOpacity.IsConstant)
        {
            brush.SetFixOpacity(paint.CircleOpacity.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableOpacity((context) => paint.CircleOpacity.Evaluate(context));
        }

        // circle-radius
        //   Optional number. Units in pixels. Defaults to 5. Interpolated.
        //   Circle radius.
        if (paint.CircleRadius.IsConstant)
        {
            brush.SetFixRadius(paint.CircleRadius.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableRadius((context) => paint.CircleRadius.Evaluate(context));
        }

        // circle-blur
        //   Optional number. Defaults to 0. Interpolated.
        //   Amount to blur the circle. 1 blurs the circle such that only the
        //   centerpoint has full opacity.
        if (paint.CircleBlur.IsConstant)
        {
            brush.SetFixBlur(paint.CircleBlur.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableBlur((context) => paint.CircleBlur.Evaluate(context));
        }

        // circle-stroke-color
        //   Optional color. Defaults to #000000. Interpolated.
        //   The stroke color of the circle.
        if (paint.CircleStrokeColor.IsConstant)
        {
            brush.SetFixOutlineColor(paint.CircleStrokeColor.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableOutlineColor((context) => paint.CircleStrokeColor.Evaluate(context));
        }

        // circle-stroke-opacity
        //   Optional number. Defaults to 1. Interpolated.
        //   The opacity of the circle's stroke. Circle stroke opacity is
        //   multiplied with circle-opacity.
        if (paint.CircleStrokeOpacity.IsConstant)
        {
            brush.SetFixOutlineOpacity(paint.CircleStrokeOpacity.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableOutlineOpacity((context) => paint.CircleStrokeOpacity.Evaluate(context));
        }

        // circle-stroke-width
        //   Optional number. Units in pixels. Defaults to 0. Interpolated.
        //   The width of the circle's stroke. Strokes are placed outside of
        //   the circle-radius.
        if (paint.CircleStrokeWidth.IsConstant)
        {
            brush.SetFixStrokeWidth(paint.CircleStrokeWidth.Evaluate(EvaluationContext.Empty));
        }
        else
        {
            brush.SetVariableStrokeWidth((context) => paint.CircleStrokeWidth.Evaluate(context));
        }

        // circle-translate
        //   Optional array. Units in pixels. Defaults to 0,0. Interpolated.
        // circle-translate-anchor
        //   Optional enum. One of map, viewport. Defaults to map.
        //   TODO: Use matrix of paint object for this

        // circle-pitch-alignment / circle-pitch-scale / circle-emissive-strength
        //   Only relevant for 3D rendering, which isn't supported here.

        return brush;
    }
}
