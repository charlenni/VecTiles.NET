using VecTiles.Common.Enums;
using VecTiles.Common.Extensions;
using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

/// <summary>
/// Style file independent paint that evaluates all paint properties for a feature and zoom level,
/// either from fixed values or from variable functions given as evaluation functions.
/// </summary>
public class Paint : IPaint
{
    EvaluationContext? _lastContext;
    float _strokeWidth;

    float _radius;

    float _blur;

    /// <summary>
    /// Initializes a new paint with the given id.
    /// </summary>
    /// <param name="id">The id of the paint, adopted from the corresponding layer.</param>
    public Paint(string id)
    {
        Id = id;
    }

    // Id of layer for this OMTPaint
    /// <summary>
    /// Gets the id of the layer for this paint.
    /// </summary>
    public string Id { get; }

    /// <inheritdoc/>
    public PaintStyle Style { get; private set; }

    /// <inheritdoc/>
    public Color Color { get; private set; }

    /// <inheritdoc/>
    public Color OutlineColor { get; private set; } = Colors.Black;

    /// <inheritdoc/>
    public float Opacity => opacity;

    /// <inheritdoc/>
    public bool IsAntialias { get; private set; } = true;

    /// <inheritdoc/>
    public float StrokeWidth { get; private set; }

    /// <inheritdoc/>
    public StrokeCap StrokeCap { get; private set; }

    /// <inheritdoc/>
    public StrokeJoin StrokeJoin { get; private set; }

    /// <inheritdoc/>
    public float StrokeMiter { get; private set; }

    /// <inheritdoc/>
    public ISprite? Pattern { get; private set; }

    /// <inheritdoc/>
    public float[]? DashArray { get; private set; }

    /// <inheritdoc/>
    public float Radius { get; private set; }

    /// <inheritdoc/>
    public float Blur { get; private set; }

    /// <summary>
    /// Evaluates all variable paint properties for the given context and updates its fixed properties accordingly.
    /// </summary>
    /// <param name="context">The evaluation context with zoom information and feature attributes.</param>
    public void Update(EvaluationContext context)
    {
        if (_lastContext != null && context.Equals(_lastContext))
            return;

        if (variableColor || variableOpacity)
        {
            var c = variableColor && funcColor != null ? funcColor(context) : color;
            var o = variableOpacity && funcOpacity != null ? funcOpacity(context) : opacity;
            Color = c.WithAlpha((byte)(c.A * o));
        }

        if (variableOutlineColor || variableOpacity || variableOutlineOpacity)
        {
            var c = variableOutlineColor && funcOutlineColor != null ? funcOutlineColor(context) : outlineColor;
            var o = variableOutlineOpacity && funcOutlineOpacity != null ?
                funcOutlineOpacity(context) : _outlineOpacity;

            if (variableOpacity && funcOpacity != null)
            {
                o *= funcOpacity(context);
            }
            else
            {
                o *= opacity;
            }

            OutlineColor = c.WithAlpha((byte)(c.A * o));
        }

        if (variableStyle && funcStyle != null)
        {
            Style = funcStyle(context);
        }

        if (variableAntialias && funcAntialias != null)
        {
            IsAntialias = funcAntialias(context);
        }

        if (variableStrokeWidth && funcStrokeWidth != null)
        {
            StrokeWidth = funcStrokeWidth(context) * context.Scale;
        }
        else
        {
            StrokeWidth = _strokeWidth * context.Scale;
        }

        if (variableRadius && funcRadius != null)
        {
            Radius = funcRadius(context) * context.Scale;
        }
        else
        {
            Radius = _radius * context.Scale;
        }

        if (variableBlur && funcBlur != null)
        {
            Blur = funcBlur(context) * context.Scale;
        }
        else
        {
            Blur = _blur * context.Scale;
        }

        if (variableStrokeCap && funcStrokeCap != null)
        {
            StrokeCap = funcStrokeCap(context);
        }

        if (variableStrokeJoin && funcStrokeJoin != null)
        {
            StrokeJoin = funcStrokeJoin(context);
        }

        if (variableStrokeMiter && funcStrokeMiter != null)
        {
            StrokeMiter = funcStrokeMiter(context);
        }

        if (variablePattern)
        {
            if (funcISpritePattern != null)
            {
                Pattern = funcISpritePattern(context);
            }
            else if (funcPattern != null)
            {
                Pattern = funcPattern(context);
            }
        }

        if (variableDashArray && funcDashArray != null)
        {
            DashArray = funcDashArray(context);
        }
        else if (fixDashArray != null && fixDashArray.Length > 0)
        {
            DashArray = fixDashArray;
        }
        else 
        {
            DashArray = null;
        }

        _lastContext = new EvaluationContext(context.Zoom, context.Scale, context.Rotation, context.Attributes);

        return;
    }

    /// <summary>
    /// Gets the maximum stroke width for the given context, i.e. the maximum of the evaluated
    /// stroke width at the context's zoom and at the next integer zoom level.
    /// </summary>
    /// <param name="context">The evaluation context with zoom information.</param>
    /// <returns>The maximum stroke width in style px.</returns>
    public float GetMaxStrokeWidth(EvaluationContext context)
    {
        if (!variableStrokeWidth)
            return _strokeWidth;
        
        // Calc MaxStrokeWidth for variable stroke width
        return Math.Max(Math.Max(
            funcStrokeWidth!(context), 
            funcStrokeWidth!(new EvaluationContext(context.Zoom + 1))),
            0.1f);
    }

    /// <summary>
    /// Gets the stroke width obtained when the stroke width function is evaluated at the
    /// nearest lower integer zoom level of the context.
    /// </summary>
    /// <param name="context">The evaluation context with zoom information.</param>
    /// <returns>The stroke width evaluated at the integer zoom level.</returns>
    public float GetStrokeWidthFromIntZoom(EvaluationContext context)
    {
        if (!variableStrokeWidth)
            return _strokeWidth;

        var intContext = new EvaluationContext((int)context.Zoom);
        
        // Calc MaxStrokeWidth for variable stroke width
        return funcStrokeWidth!(intContext); 
    }

    #region Color

    Color color = Colors.Empty;

    bool variableColor = false;

    Func<EvaluationContext, Color>? funcColor;

    /// <summary>
    /// Sets the fill color to a fixed value.
    /// </summary>
    /// <param name="c">The fixed fill color.</param>
    public void SetFixColor(Color c)
    {
        variableColor = false;
        color = c;
        Color = color.WithAlpha((byte)(color.A * opacity));
    }

    /// <summary>
    /// Sets the fill color from a function evaluated per feature.
    /// </summary>
    /// <param name="func">The function that calculates the fill color for an evaluation context.</param>
    public void SetVariableColor(Func<EvaluationContext, Color> func)
    {
        variableColor = true;
        funcColor = func;
    }

    #endregion

    #region OutlineColor

    Color outlineColor = Colors.Empty;

    float _outlineOpacity = 1.0f;

    bool variableOutlineColor = false;

    bool variableOutlineOpacity = false;

    Func<EvaluationContext, Color>? funcOutlineColor;

    Func<EvaluationContext, float>? funcOutlineOpacity;

    /// <summary>
    /// Sets the outline color to a fixed value.
    /// </summary>
    /// <param name="c">The fixed outline color.</param>
    public void SetFixOutlineColor(Color c)
    {
        variableOutlineColor = false;
        outlineColor = c;
        OutlineColor = outlineColor.WithAlpha((byte)(outlineColor.A * opacity * _outlineOpacity));
    }

    /// <summary>
    /// Sets the outline color from a function evaluated per feature.
    /// </summary>
    /// <param name="func">The function that calculates the outline color for an evaluation context.</param>
    public void SetVariableOutlineColor(Func<EvaluationContext, Color> func)
    {
        variableOutlineColor = true;
        funcOutlineColor = func;
    }

    /// <summary>
    /// Sets the outline opacity to a fixed value.
    /// </summary>
    /// <param name="o">The fixed outline opacity between 0 and 1.</param>
    public void SetFixOutlineOpacity(float o)
    {
        variableOutlineOpacity = false;
        _outlineOpacity = o;
        OutlineColor = outlineColor.WithAlpha((byte)(outlineColor.A * opacity * _outlineOpacity));
    }

    /// <summary>
    /// Sets the outline opacity from a function evaluated per feature.
    /// </summary>
    /// <param name="func">The function that calculates the outline opacity for an evaluation context.</param>
    public void SetVariableOutlineOpacity(Func<EvaluationContext, float> func)
    {
        variableOutlineOpacity = true;
        funcOutlineOpacity = func;
    }

    #endregion

    #region Opacity

    float opacity = 1.0f;

    bool variableOpacity = false;

    Func<EvaluationContext, float>? funcOpacity;

    /// <summary>
    /// Sets the opacity to a fixed value.
    /// </summary>
    /// <param name="o">The fixed opacity between 0 (transparent) and 1 (opaque).</param>
    public void SetFixOpacity(float o)
    {
        variableOpacity = false;
        opacity = o;
        Color = color.WithAlpha((byte)(color.A * opacity));
    }

    /// <summary>
    /// Sets the opacity from a function evaluated per feature.
    /// </summary>
    /// <param name="func">The function that calculates the opacity for an evaluation context.</param>
    public void SetVariableOpacity(Func<EvaluationContext, float> func)
    {
        variableOpacity = true;
        funcOpacity = func;
    }

    #endregion

    #region Style

    bool variableStyle = false;

    Func<EvaluationContext, PaintStyle>? funcStyle;

    /// <summary>
    /// Sets the painting style to a fixed value.
    /// </summary>
    /// <param name="style">The fixed <see cref="PaintStyle"/>.</param>
    public void SetFixStyle(PaintStyle style)
    {
        variableStyle = false;
        Style = style;
    }

    /// <summary>
    /// Sets the painting style from a function evaluated per feature.
    /// </summary>
    /// <param name="func">The function that calculates the <see cref="PaintStyle"/> for an evaluation context.</param>
    public void SetVariableStyle(Func<EvaluationContext, PaintStyle> func)
    {
        variableStyle = true;
        funcStyle = func;
    }

    #endregion

    #region Antialias

    bool variableAntialias = false;

    Func<EvaluationContext, bool>? funcAntialias;

    /// <summary>
    /// Sets the antialiasing flag to a fixed value.
    /// </summary>
    /// <param name="antialias">Whether antialiasing is used.</param>
    public void SetFixAntialias(bool antialias)
    {
        variableAntialias = false;
        IsAntialias = antialias;
    }

    /// <summary>
    /// Sets the antialiasing flag from a function evaluated per feature.
    /// </summary>
    /// <param name="func">The function that calculates the antialiasing flag for an evaluation context.</param>
    public void SetVariableAntialias(Func<EvaluationContext, bool> func)
    {
        variableAntialias = true;
        funcAntialias = func;
    }

    #endregion

    #region StrokeWidth

    bool variableStrokeWidth = false;

    Func<EvaluationContext, float>? funcStrokeWidth;

    /// <summary>
    /// Sets the stroke width to a fixed value in style px.
    /// </summary>
    /// <param name="width">The fixed stroke width in style px.</param>
    public void SetFixStrokeWidth(float width)
    {
        variableStrokeWidth = false;
        _strokeWidth = width;
    }

    /// <summary>
    /// Sets the stroke width from a function evaluated per context, scaled by the context's scale factor.
    /// </summary>
    /// <param name="func">The function that calculates the stroke width for an evaluation context.</param>
    public void SetVariableStrokeWidth(Func<EvaluationContext, float> func)
    {
        variableStrokeWidth = true;
        funcStrokeWidth = func;
    }

    #endregion

    #region StrokeCap

    bool variableStrokeCap = false;

    Func<EvaluationContext, StrokeCap>? funcStrokeCap;

    /// <summary>
    /// Sets the stroke cap to a fixed value.
    /// </summary>
    /// <param name="cap">The fixed <see cref="StrokeCap"/>.</param>
    public void SetFixStrokeCap(StrokeCap cap)
    {
        variableStrokeCap = false;
        StrokeCap = cap;
    }

    /// <summary>
    /// Sets the stroke cap from a function evaluated per context.
    /// </summary>
    /// <param name="func">The function that calculates the <see cref="StrokeCap"/> for an evaluation context.</param>
    public void SetVariableStrokeCap(Func<EvaluationContext, StrokeCap> func)
    {
        variableStrokeCap = true;
        funcStrokeCap = func;
    }

    #endregion

    #region StrokeJoin

    bool variableStrokeJoin = false;

    Func<EvaluationContext, StrokeJoin>? funcStrokeJoin;

    /// <summary>
    /// Sets the stroke join to a fixed value.
    /// </summary>
    /// <param name="join">The fixed <see cref="StrokeJoin"/>.</param>
    public void SetFixStrokeJoin(StrokeJoin join)
    {
        variableStrokeJoin = false;
        StrokeJoin = join;
    }

    /// <summary>
    /// Sets the stroke join from a function evaluated per context.
    /// </summary>
    /// <param name="func">The function that calculates the <see cref="StrokeJoin"/> for an evaluation context.</param>
    public void SetVariableStrokeJoin(Func<EvaluationContext, StrokeJoin> func)
    {
        variableStrokeJoin = true;
        funcStrokeJoin = func;
    }

    #endregion

    #region StrokeMiter

    bool variableStrokeMiter = false;

    Func<EvaluationContext, float>? funcStrokeMiter;

    /// <summary>
    /// Sets the stroke miter limit to a fixed value.
    /// </summary>
    /// <param name="miter">The fixed stroke miter limit.</param>
    public void SetFixStrokeMiter(float miter)
    {
        variableStrokeMiter = false;
        StrokeMiter = miter;
    }

    /// <summary>
    /// Sets the stroke miter limit from a function evaluated per context.
    /// </summary>
    /// <param name="func">The function that calculates the stroke miter limit for an evaluation context.</param>
    public void SetVariableStrokeMiter(Func<EvaluationContext, float> func)
    {
        variableStrokeMiter = true;
        funcStrokeMiter = func;
    }

    #endregion

    #region Pattern

    bool variablePattern = false;

    Func<EvaluationContext, Sprite>? funcPattern;

    /// <summary>
    /// Sets the pattern sprite to a fixed value.
    /// </summary>
    /// <param name="sprite">The fixed pattern sprite.</param>
    public void SetFixPattern(Sprite sprite)
    {
        variablePattern = false;
        Pattern = sprite;
    }

    /// <summary>
    /// Sets the pattern sprite to a fixed value, accepting any implementation
    /// of <see cref="ISprite"/> (also vector sprites, e.g. of SVG patterns).
    /// </summary>
    /// <param name="sprite">The fixed pattern sprite.</param>
    public void SetFixPattern(ISprite sprite)
    {
        variablePattern = false;
        Pattern = sprite;
    }

    /// <summary>
    /// Sets the pattern sprite from a function evaluated per context.
    /// </summary>
    /// <param name="func">The function that calculates the pattern sprite for an evaluation context.</param>
    public void SetVariablePattern(Func<EvaluationContext, Sprite> func)
    {
        variablePattern = true;
        funcPattern = func;
    }

    Func<EvaluationContext, ISprite>? funcISpritePattern;

    /// <summary>
    /// Sets the pattern sprite from a function evaluated per context,
    /// accepting any implementation of <see cref="ISprite"/> (also vector
    /// sprites, e.g. of SVG patterns).
    /// </summary>
    /// <param name="func">The function that calculates the pattern sprite for an evaluation context.</param>
    public void SetVariablePattern(Func<EvaluationContext, ISprite> func)
    {
        variablePattern = true;
        funcISpritePattern = func;
    }

    #endregion

    #region DashArray

    bool variableDashArray = false;
    float[] fixDashArray = [];

    Func<EvaluationContext, float[]>? funcDashArray;

    /// <summary>
    /// Sets the dash array to a fixed value.
    /// </summary>
    /// <param name="array">The fixed array of dash and gap lengths alternating between them.</param>
    public void SetFixDashArray(float[] array)
    {
        variableDashArray = false;
        fixDashArray = new float[array.Length];
        for (int i = 0; i < array.Length; i++)
            fixDashArray[i] = array[i];
    }

    /// <summary>
    /// Sets the dash array from a function evaluated per context.
    /// </summary>
    /// <param name="func">The function that calculates the dash array for an evaluation context.</param>
    public void SetVariableDashArray(Func<EvaluationContext, float[]> func)
    {
        variableDashArray = true;
        funcDashArray = func;
    }

    #endregion

    #region Radius

    bool variableRadius = false;

    Func<EvaluationContext, float>? funcRadius;

    /// <summary>
    /// Sets the circle radius to a fixed value in style px.
    /// </summary>
    /// <param name="radius">The fixed circle radius in style px.</param>
    public void SetFixRadius(float radius)
    {
        variableRadius = false;
        _radius = radius;
    }

    /// <summary>
    /// Sets the circle radius from a function evaluated per context, scaled by the context's scale factor.
    /// </summary>
    /// <param name="func">The function that calculates the circle radius for an evaluation context.</param>
    public void SetVariableRadius(Func<EvaluationContext, float> func)
    {
        variableRadius = true;
        funcRadius = func;
    }

    #endregion

    #region Blur

    bool variableBlur = false;

    Func<EvaluationContext, float>? funcBlur;

    /// <summary>
    /// Sets the circle blur to a fixed value in style px.
    /// </summary>
    /// <param name="blur">The fixed circle blur in style px.</param>
    public void SetFixBlur(float blur)
    {
        variableBlur = false;
        _blur = blur;
    }

    /// <summary>
    /// Sets the circle blur from a function evaluated per context, scaled by the context's scale factor.
    /// </summary>
    /// <param name="func">The function that calculates the circle blur for an evaluation context.</param>
    public void SetVariableBlur(Func<EvaluationContext, float> func)
    {
        variableBlur = true;
        funcBlur = func;
    }

    #endregion
}
