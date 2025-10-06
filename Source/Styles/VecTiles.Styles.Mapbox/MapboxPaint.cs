using VecTiles.Common.Enums;
using VecTiles.Common.Extensions;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox;

public class MapboxPaint : IPaint
{
    EvaluationContext? _lastContext;
    float _strokeWidth;

    public MapboxPaint(string id)
    {
        Id = id;
    }

    // Id of layer for this OMTPaint
    public string Id { get; }

    public PaintStyle Style { get; private set; }

    public Color Color { get; private set; }

    public Color OutlineColor { get; private set; }

    public float Opacity { get; private set; }

    public bool IsAntialias { get; private set; }

    public float StrokeWidth { get; private set; }

    public StrokeCap StrokeCap { get; private set; }

    public StrokeJoin StrokeJoin { get; private set; }

    public float StrokeMiter { get; private set; }

    public ISprite Pattern { get; private set; }

    public float[]? DashArray { get; private set; }

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

        if (variableOutlineColor || variableOpacity)
        {
            var c = variableOutlineColor && funcOutlineColor != null ? funcOutlineColor(context) : outlineColor;
            var o = variableOpacity && funcOpacity != null ? funcOpacity(context) : opacity;
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

        // We have to multiply the dasharray with the linewidth
        if (variableDashArray && funcDashArray != null)
        {
            var array = funcDashArray(context);
            for (var i = 0; i < array.Length; i++)
                array[i] = array[i] * StrokeWidth;
            DashArray = array;
        }
        else if (fixDashArray != null && fixDashArray.Length > 0)
        {
            var array = new float[fixDashArray.Length];
            for (var i = 0; i < array.Length; i++)
                array[i] = fixDashArray[i] * StrokeWidth;
            DashArray = array;
        }
        else 
        {
            DashArray = null;
        }

        _lastContext = new EvaluationContext(context.Zoom, context.Scale, context.Rotation, context.Attributes);

        return;
    }

    #region Color

    Color color = Color.Empty;

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

    Color outlineColor = Color.Empty;

    bool variableOutlineColor = false;

    Func<EvaluationContext, Color>? funcOutlineColor;

    public void SetFixOutlineColor(Color c)
    {
        variableOutlineColor = false;
        outlineColor = c;
        Color = outlineColor.WithAlpha((byte)(outlineColor.A * opacity));
    }

    public void SetVariableOutlineColor(Func<EvaluationContext, Color> func)
    {
        variableOutlineColor = true;
        funcOutlineColor = func;
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

    Func<EvaluationContext, MapboxSprite>? funcPattern;

    public void SetFixPattern(MapboxSprite sprite)
    {
        variablePattern = false;
        Pattern = sprite;
    }

    public void SetVariablePattern(Func<EvaluationContext, MapboxSprite> func)
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
}
