using VecTiles.Common.Enums;
using VecTiles.Common.Extensions;
using VecTiles.Common.Interfaces;

namespace VecTiles.Common.Primitives;

public class Paint : IPaint
{
    EvaluationContext? _lastContext;
    float _strokeWidth;

    float _radius;

    float _blur;

    public Paint(string id)
    {
        Id = id;
    }

    // Id of layer for this OMTPaint
    public string Id { get; }

    public PaintStyle Style { get; private set; }

    public Color Color { get; private set; }

    public Color OutlineColor { get; private set; } = Colors.Black;

    public float Opacity => opacity;

    public bool IsAntialias { get; private set; } = true;

    public float StrokeWidth { get; private set; }

    public StrokeCap StrokeCap { get; private set; }

    public StrokeJoin StrokeJoin { get; private set; }

    public float StrokeMiter { get; private set; }

    public ISprite? Pattern { get; private set; }

    public float[]? DashArray { get; private set; }

    public float Radius { get; private set; }

    public float Blur { get; private set; }

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

        if (variablePattern && funcPattern != null)
        {
            Pattern = funcPattern(context);
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

    public void SetFixColor(Color c)
    {
        variableColor = false;
        color = c;
        Color = color.WithAlpha((byte)(color.A * opacity));
    }

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

    public void SetFixOutlineColor(Color c)
    {
        variableOutlineColor = false;
        outlineColor = c;
        OutlineColor = outlineColor.WithAlpha((byte)(outlineColor.A * opacity * _outlineOpacity));
    }

    public void SetVariableOutlineColor(Func<EvaluationContext, Color> func)
    {
        variableOutlineColor = true;
        funcOutlineColor = func;
    }

    public void SetFixOutlineOpacity(float o)
    {
        variableOutlineOpacity = false;
        _outlineOpacity = o;
        OutlineColor = outlineColor.WithAlpha((byte)(outlineColor.A * opacity * _outlineOpacity));
    }

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

    public void SetFixOpacity(float o)
    {
        variableOpacity = false;
        opacity = o;
        Color = color.WithAlpha((byte)(color.A * opacity));
    }

    public void SetVariableOpacity(Func<EvaluationContext, float> func)
    {
        variableOpacity = true;
        funcOpacity = func;
    }

    #endregion

    #region Style

    bool variableStyle = false;

    Func<EvaluationContext, PaintStyle>? funcStyle;

    public void SetFixStyle(PaintStyle style)
    {
        variableStyle = false;
        Style = style;
    }

    public void SetVariableStyle(Func<EvaluationContext, PaintStyle> func)
    {
        variableStyle = true;
        funcStyle = func;
    }

    #endregion

    #region Antialias

    bool variableAntialias = false;

    Func<EvaluationContext, bool>? funcAntialias;

    public void SetFixAntialias(bool antialias)
    {
        variableAntialias = false;
        IsAntialias = antialias;
    }

    public void SetVariableAntialias(Func<EvaluationContext, bool> func)
    {
        variableAntialias = true;
        funcAntialias = func;
    }

    #endregion

    #region StrokeWidth

    bool variableStrokeWidth = false;

    Func<EvaluationContext, float>? funcStrokeWidth;

    public void SetFixStrokeWidth(float width)
    {
        variableStrokeWidth = false;
        _strokeWidth = width;
    }

    public void SetVariableStrokeWidth(Func<EvaluationContext, float> func)
    {
        variableStrokeWidth = true;
        funcStrokeWidth = func;
    }

    #endregion

    #region StrokeCap

    bool variableStrokeCap = false;

    Func<EvaluationContext, StrokeCap>? funcStrokeCap;

    public void SetFixStrokeCap(StrokeCap cap)
    {
        variableStrokeCap = false;
        StrokeCap = cap;
    }

    public void SetVariableStrokeCap(Func<EvaluationContext, StrokeCap> func)
    {
        variableStrokeCap = true;
        funcStrokeCap = func;
    }

    #endregion

    #region StrokeJoin

    bool variableStrokeJoin = false;

    Func<EvaluationContext, StrokeJoin>? funcStrokeJoin;

    public void SetFixStrokeJoin(StrokeJoin join)
    {
        variableStrokeJoin = false;
        StrokeJoin = join;
    }

    public void SetVariableStrokeJoin(Func<EvaluationContext, StrokeJoin> func)
    {
        variableStrokeJoin = true;
        funcStrokeJoin = func;
    }

    #endregion

    #region StrokeMiter

    bool variableStrokeMiter = false;

    Func<EvaluationContext, float>? funcStrokeMiter;

    public void SetFixStrokeMiter(float miter)
    {
        variableStrokeMiter = false;
        StrokeMiter = miter;
    }

    public void SetVariableStrokeMiter(Func<EvaluationContext, float> func)
    {
        variableStrokeMiter = true;
        funcStrokeMiter = func;
    }

    #endregion

    #region Pattern

    bool variablePattern = false;

    Func<EvaluationContext, Sprite>? funcPattern;

    public void SetFixPattern(Sprite sprite)
    {
        variablePattern = false;
        Pattern = sprite;
    }

    public void SetVariablePattern(Func<EvaluationContext, Sprite> func)
    {
        variablePattern = true;
        funcPattern = func;
    }

    #endregion

    #region DashArray

    bool variableDashArray = false;
    float[] fixDashArray = [];

    Func<EvaluationContext, float[]>? funcDashArray;

    public void SetFixDashArray(float[] array)
    {
        variableDashArray = false;
        fixDashArray = new float[array.Length];
        for (int i = 0; i < array.Length; i++)
            fixDashArray[i] = array[i];
    }

    public void SetVariableDashArray(Func<EvaluationContext, float[]> func)
    {
        variableDashArray = true;
        funcDashArray = func;
    }

    #endregion

    #region Radius

    bool variableRadius = false;

    Func<EvaluationContext, float>? funcRadius;

    public void SetFixRadius(float radius)
    {
        variableRadius = false;
        _radius = radius;
    }

    public void SetVariableRadius(Func<EvaluationContext, float> func)
    {
        variableRadius = true;
        funcRadius = func;
    }

    #endregion

    #region Blur

    bool variableBlur = false;

    Func<EvaluationContext, float>? funcBlur;

    public void SetFixBlur(float blur)
    {
        variableBlur = false;
        _blur = blur;
    }

    public void SetVariableBlur(Func<EvaluationContext, float> func)
    {
        variableBlur = true;
        funcBlur = func;
    }

    #endregion
}
