using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox;

public class MapboxTextPointSymbol : MapboxSymbol
{
    static Dictionary<string, Topten.RichTextKit.Style> _textStyles = new();
    static SKPaint _debugPaint = new SKPaint { Color = SKColors.Blue, StrokeWidth = 1, IsStroke = true };

    Topten.RichTextKit.Style _textStyle;
    float _measuredWidth;
    float _measuredHeight;
    float _leftRightCorrection;
    EvaluationContext _lastContext;
    Envelope _lastEnvelope;

    public MapboxTextPointSymbol(Tile tile, Point point, TextBlock textBlock) : base(tile)
    {
        Point = point;
        Text = textBlock;

        _textStyle = (Topten.RichTextKit.Style)textBlock.GetStyleAtOffset(0);
    }

    /// <summary>
    /// Point where symbol is placed in world coordinates
    /// </summary>
    public Point Point { get; }

    /// <summary>
    /// Text block for this symbol
    /// </summary>
    public TextBlock Text { get; }

    /// <summary>
    /// Is symbol optional?
    /// </summary>
    public bool Optional { get; internal set; }

    /// <summary>
    /// Allow other symbols to overlap this symbol
    /// </summary>
    public bool AllowOverlap { get; internal set; }

    /// <summary>
    /// Rotation of symbol in degrees
    /// </summary>
    public float Rotation { get; internal set; }

    /// <summary>
    /// Keep the text upright, so that is easier to read
    /// </summary>
    public bool KeepUpright { get; internal set; }

    /// <summary>
    /// Anchor of symbol given as relative position with [0..1, 0..1]
    /// </summary>
    public SKPoint Anchor { get; internal set; }

    /// <summary>
    /// Offset from point in pixels
    /// </summary>
    public SKPoint Offset { get; internal set; }

    /// <summary>
    /// Function to calculate color to use when drawing symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, SKColor>? Color { get; internal set; }

    /// <summary>
    /// Function to calculate opacity of symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? Opacity { get; internal set; }

    /// <summary>
    /// Function to calculate halo color to use when drawing symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, SKColor>? HaloColor { get; internal set; }

    /// <summary>
    /// Function to calculate halo blur to use when drawing symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? HaloBlur { get; internal set; }

    /// <summary>
    /// Function to calculate halo width to use when drawing symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? HaloWidth { get; internal set; }

    /// <summary>
    /// Function to calculate translate of symbols point from EvaluationContext in pixels
    /// </summary>
    public Func<EvaluationContext, SKPoint>? Translate { get; internal set; }

    /// <summary>
    /// Function to calculate anchor of translate (map or viewport) from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, MapAlignment>? TranslateAnchor { get; internal set; }

    public Func<EvaluationContext, float, float> MaxWidth;

    public override bool CheckForSpace(SKCanvas canvas, EvaluationContext context, Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter, bool showValidBorders = false, bool showUnvalidBorders = false)
    {
        (var screenX, var screenY) = worldToScreenConverter(Point.X, Point.Y);

        Envelope = CreateEnvelope(canvas, context, screenX, screenY);

        var symbols = tree.Query(Envelope);

        foreach (var symbol in symbols)
        {
            if (((MapboxSymbol)symbol).Envelope == null)
            {
                // Should not happen
                continue;
            }

            if (!Envelope.Intersects(((MapboxSymbol)symbol).Envelope))
            {
                continue;
            }

            if (!symbol.AllowOthers)
            {
                if (showUnvalidBorders && symbol.Name != Name)
                {
                    canvas.DrawRect(new SKRect((float)Envelope.MinX, (float)Envelope.MaxY, (float)Envelope.MaxX, (float)Envelope.MinY), _debugPaint);
                }

                return false;
            }
        }

        if (showValidBorders)
        {
            canvas.DrawRect(new SKRect((float)Envelope.MinX, (float)Envelope.MaxY, (float)Envelope.MaxX, (float)Envelope.MinY), _debugPaint);
        }

        return true;
    }

    public override void Draw(SKCanvas canvas, EvaluationContext context, ref Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter)
    {
        //var envelope = CreateEnvelope(canvas, context, screenX, screenY);
        (var screenX, var screenY) = worldToScreenConverter(Point.X, Point.Y);

        if (!Envelope.IsNull)
        {
            DrawText(canvas, context, screenX, screenY);

            tree.Insert(Envelope, this);
        }
    }

    private void DrawText(SKCanvas canvas, EvaluationContext context, double screenX, double screenY)
    {
        canvas.Save();

        canvas.Translate((float)screenX, (float)screenY);

        if (Translate != null)
        {
            var translate = Translate?.Invoke(context) ?? new SKPoint(0, 0);
            var translateAnchor = TranslateAnchor?.Invoke(context) ?? MapAlignment.Map;

            if (translateAnchor == MapAlignment.Viewport)
            {
                canvas.RotateDegrees(-context.Rotation);
            }

            canvas.Translate(translate);
        }

        canvas.Scale(1f / context.Scale);
        canvas.Translate(Offset);
        canvas.RotateDegrees(Rotation);

        _textStyle.TextColor = Color.Invoke(context).WithAlpha((byte)(Opacity.Invoke(context) * 255f));
        _textStyle.HaloBlur = HaloBlur.Invoke(context);
        _textStyle.HaloColor = HaloColor.Invoke(context);
        _textStyle.HaloWidth = HaloWidth.Invoke(context);

        Text.ApplyStyle(0, Text.Length, _textStyle);

        // Translate to right possition respecting the correction (leading space before TextBlocks text starts)
        canvas.Translate(Anchor.X * _measuredWidth - _leftRightCorrection, Anchor.Y * _measuredHeight);

        Text.Paint(canvas);

        canvas.Restore();
    }

    private Envelope CreateEnvelope(SKCanvas canvas, EvaluationContext context, double screenX, double screenY)
    {
        if (Text == null)
        {
            return new Envelope();
        }

        if (_lastContext != null 
            && context.Zoom == _lastContext.Zoom 
            && context.Scale == _lastContext.Scale 
            && context.Rotation == _lastContext.Rotation)
        {
            // Nothing changed, so return the last envelope
            return _lastEnvelope;
        }

        // Set max width to the correct value
        Text.MaxWidth = MaxWidth.Invoke(context, _textStyle.FontSize);
        Text.ApplyStyle(0, Text.Length, _textStyle);

        // MeasuredWidth and MeasuredHeight need many CPU cycles, so save them for later use
        _measuredWidth = Text.MeasuredWidth;
        _measuredHeight = Text.MeasuredHeight;
        // MaxWidth could be greater than MeasuredWidth. Then there is some space around the text.
        // To save another call to MeasuredWidth (with another time consuming layout), we save the 
        // amount of space in front of the text and remove this when drawing the text.
        _leftRightCorrection = (float)(Text.MaxWidth - _measuredWidth) * Text.Alignment switch
        {
            TextAlignment.Left => 0f,
            TextAlignment.Center => 0.5f,
            TextAlignment.Right => 1.0f,
            TextAlignment.Auto => Text.BaseDirection switch
            {
                TextDirection.LTR => 0.0f,
                TextDirection.RTL => 1.0f,
                _ => 0.0f  // TODO: Not sure what happens with TextDirection.Auto
            },
            _ => 0.0f
        };

        var anchor = new SKPoint(Anchor.X * _measuredWidth, Anchor.Y * _measuredHeight);
        var offset = new SKPoint(anchor.X + Offset.X, anchor.Y + Offset.Y);

        // We now could calc the rughly envelope of text
        var envelope = new Envelope(0 + offset.X, _measuredWidth + offset.X, 0 + offset.Y, _measuredHeight + offset.Y);

        if (Rotation != 0.0)
        {
            envelope.RotateDegrees(Rotation);
        }

        envelope.Translate(screenX, screenY);

        if (Translate != null)
        {
            var translate = Translate?.Invoke(context) ?? new SKPoint(0, 0);
            var translateAnchor = TranslateAnchor?.Invoke(context) ?? MapAlignment.Map;

            if (translateAnchor == MapAlignment.Map)
            {
                var rotation = context.Rotation * Math.PI / 180.0;
                var cos = Math.Cos(rotation);
                var sin = Math.Sin(rotation);
                var x = translate.X * cos - translate.Y * sin;
                var y = translate.X * sin + translate.Y * cos;
                translate = new SKPoint((float)x, (float)y);
            }

            envelope.Translate(translate.X, translate.Y);
        }

        _lastContext = context;
        _lastEnvelope = envelope;

        return envelope;
    }
}
