using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapbox.Extensions;

namespace VecTiles.Styles.Mapbox;

public class MapboxIconPointSymbol : MapboxSymbol
{
    readonly SKPaint _paint = new SKPaint();
    static SKPaint _debugPaint = new SKPaint { Color = Color.Green, StrokeWidth = 1, IsStroke = true };

    public MapboxIconPointSymbol(Tile tile, Point point, ISprite sprite) : base(tile)
    {
        Point = point;
        Icon = sprite;
    }

    /// <summary>
    /// Point where symbol is placed in world coordinates
    /// </summary>
    public Point Point { get; }

    /// <summary>
    /// ISprite to use for this symbol
    /// </summary>
    public ISprite Icon { get; }

    /// <summary>
    /// Is symbol optional?
    /// </summary>
    public bool Optional { get; internal set; }

    /// <summary>
    /// Allow other symbols to overlap this symbol
    /// </summary>
    public bool AllowOverlap { get; internal set; }

    /// <summary>
    /// Scale of symbol
    /// </summary>
    public float Scale { get; internal set; }

    /// <summary>
    /// Rotation of symbol in degrees
    /// </summary>
    public float Rotation { get; internal set; }

    /// <summary>
    /// Padding around symbol in pixel
    /// </summary>
    public int Padding { get; internal set; }

    /// <summary>
    /// Anchor of symbol given as relative position with [0..1, 0..1]
    /// </summary>
    public Point Anchor { get; internal set; }

    /// <summary>
    /// Offset from point in pixels
    /// </summary>
    public Point Offset { get; internal set; }

    /// <summary>
    /// Function to calculate color filter to use when drawing symbol from EvaluationContext as SKColorFilter
    /// </summary>
    public Func<EvaluationContext, SKColorFilter>? ColorFilter { get; internal set; }

    /// <summary>
    /// Function to calculate opacity of symbol from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, float>? Opacity { get; internal set; }

    /// <summary>
    /// Function to calculate translate of symbols point from EvaluationContext in pixels
    /// </summary>
    public Func<EvaluationContext, Point>? Translate { get; internal set; }

    /// <summary>
    /// Function to calculate anchor of translate (map or viewport) from EvaluationContext
    /// </summary>
    public Func<EvaluationContext, MapAlignment>? TranslateAnchor { get; internal set; }

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
                    canvas.DrawRect(new Envelope((float)Envelope.MinX, (float)Envelope.MaxY, (float)Envelope.MaxX, (float)Envelope.MinY), _debugPaint);
                }

                return false;
            }
        }

        if (showValidBorders)
        {
            canvas.DrawRect(new Envelope((float)Envelope.MinX, (float)Envelope.MaxY, (float)Envelope.MaxX, (float)Envelope.MinY), _debugPaint);
        }

        return true;
    }

    public override void Draw(SKCanvas canvas, EvaluationContext context, ref Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter)
    {
        //var envelope = CreateEnvelope(canvas, context, screenX, screenY);
        (var screenX, var screenY) = worldToScreenConverter(Point.X, Point.Y);

        if (!Envelope.IsNull)
        {
            DrawIcon(canvas, context, screenX, screenY);

            tree.Insert(Envelope, this);
        }
    }

    private void DrawIcon(SKCanvas canvas, EvaluationContext context, double screenX, double screenY)
    {
        canvas.Save();

        canvas.Translate((float)screenX, (float)screenY);

        if (ColorFilter != null)
        {
            _paint.ColorFilter = ColorFilter(context);
        }

        if (Translate != null)
        {
            var translate = Translate?.Invoke(context) ?? new Point(0, 0);
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

        canvas.Translate(Anchor.X * Icon.Width * Scale - Padding, Anchor.Y * Icon.Height * Scale - Padding);

        canvas.DrawImage(Icon, new Envelope(Padding, Padding, (float)(Icon.Width * Scale + Padding), (float)(Icon.Height * Scale + Padding)), _paint);

        canvas.Restore();
    }

    private Envelope CreateEnvelope(SKCanvas canvas, EvaluationContext context, double screenX, double screenY)
    {
        if (Icon == null)
        {
            return new Envelope();
        }

        var width = Icon.Width * Scale + Padding * 2;
        var height = Icon.Height * Scale + Padding * 2;
        var anchor = new Point(Anchor.X * width, Anchor.Y * height);
        var offset = new Point(anchor.X + Offset.X, anchor.Y + Offset.Y);

        // We now could calc the rughly envelope of icon
        var envelope = new Envelope(0 + offset.X, width + offset.X, 0 + offset.Y, height + offset.Y);

        if (Rotation != 0.0)
        {
            envelope.RotateDegrees(Rotation);
        }

        envelope.Translate(screenX, screenY);

        if (Translate != null)
        {
            var translate = Translate?.Invoke(context) ?? new Point(0, 0);
            var translateAnchor = TranslateAnchor?.Invoke(context) ?? MapAlignment.Map;

            if (translateAnchor == MapAlignment.Map)
            {
                var rotation = context.Rotation * Math.PI / 180.0;
                var cos = Math.Cos(rotation);
                var sin = Math.Sin(rotation);
                var x = translate.X * cos - translate.Y * sin;
                var y = translate.X * sin + translate.Y * cos;
                translate = new Point((float)x, (float)y);
            }

            envelope.Translate(translate.X, translate.Y);
        }

        return envelope;
    }
}
