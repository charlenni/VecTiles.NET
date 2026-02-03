using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Styles.OpenMapTiles.Extensions;

namespace VecTiles.Renderers.Skia;

public class IconLineSymbolRenderer : ISymbolRenderer
{
    private static readonly SKPaint DebugPaint = new SKPaint { Color = SKColors.Green, StrokeWidth = 1, IsStroke = true };

    public static bool CheckForSpace(SKCanvas canvas, EvaluationContext context, ISymbol sym, Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter, bool showValidBorders = false, bool showUnvalidBorders = false)
    {
        if (sym is not IconLineSymbol symbol)
        {
            return false;
        }

        var path = CreateScreenPath(symbol, worldToScreenConverter);

        // Check, if at least one symbol has space on map

        using var pathMeasure = new SKPathMeasure(path);
        
        for (var pos = 0f; pos < pathMeasure.Length; pos = pos + symbol.Spacing)
        {
            if (pathMeasure.Length < symbol.Spacing)
            {
                // We could only place one symbol in this part, so set it in the middle
                pos = pathMeasure.Length / 2;
            }
            
            pathMeasure.GetPositionAndTangent(pos, out var nextPosition, out var tangentVec);

            var tangent = 360f - Math.Atan2(tangentVec.Y, tangentVec.X) * 180 / Math.PI;
            var rotation = -symbol.Rotation;
            rotation -= (float)(symbol.RotationAlignment == MapAlignment.Map || symbol.RotationAlignment == MapAlignment.Auto ? tangent : 0.0);
            rotation %= 360;

            if (CheckForSingleSpace(canvas, context, symbol, tree, nextPosition.X, nextPosition.Y, rotation, showUnvalidBorders))
            {
                // There is at least space for one symbol
                return true;
            }
        }

        return false;
    }

    public static void Draw(SKCanvas canvas, EvaluationContext context, ISymbol sym, ref Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter)
    {
        if (sym is not IconLineSymbol symbol)
        {
            return;
        }
        
        var path = CreateScreenPath(symbol, worldToScreenConverter);

        using var pathMeasure = new SKPathMeasure(path);
        
        for (var pos = 0f; pos < pathMeasure.Length; pos = pos + symbol.Spacing)
        {
            if (pathMeasure.Length < symbol.Spacing)
            {
                // We could only place one symbol in this part, so set it in the middle
                pos = pathMeasure.Length / 2;
            }
            
            pathMeasure.GetPositionAndTangent(pos, out var nextPosition, out var tangentVec);
                
            var tangent = 360f - Math.Atan2(tangentVec.Y, tangentVec.X) * 180 / Math.PI;
            var rotation = -symbol.Rotation;
            rotation -= (float)(symbol.RotationAlignment is MapAlignment.Map or MapAlignment.Auto ? tangent : 0.0);
            rotation %= 360;

            if (!CheckForSingleSpace(canvas, context, symbol, tree, nextPosition.X, nextPosition.Y, rotation, false))
            {
                continue;
            }

            DrawIcon(canvas, context, symbol, nextPosition.X, nextPosition.Y, rotation);

            tree.Insert(symbol.Envelope, symbol);
        }
    }

    private static void DrawIcon(SKCanvas canvas, EvaluationContext context, IconLineSymbol symbol, double screenX, double screenY, double rotation)
    {
        SKPaint paint = new SKPaint();

        canvas.Save();

        canvas.Translate((float)screenX, (float)screenY);

        if (symbol.ColorFilter != null)
        {
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(symbol.ColorFilter(context));
        }

        if (symbol.Translate != null)
        {
            var translate = symbol.Translate?.Invoke(context) ?? new Point(0, 0);
            var translateAnchor = symbol.TranslateAnchor?.Invoke(context) ?? MapAlignment.Map;

            if (translateAnchor == MapAlignment.Viewport)
            {
                canvas.RotateDegrees(-context.Rotation);
            }

            canvas.Translate(translate.ToSKPoint());
        }

        canvas.Scale(1f / context.Scale);
        canvas.Translate(symbol.Offset.ToSKPoint());
        canvas.RotateDegrees((float)rotation);

        canvas.Translate((float)(symbol.Anchor.X * symbol.Icon.Width * symbol.Scale - symbol.Padding), (float)(symbol.Anchor.Y * symbol.Icon.Height * symbol.Scale - symbol.Padding));

        symbol.Icon.Atlas.Native ??= SKImage.FromEncodedData(symbol.Icon.Atlas.Binary);
        symbol.Icon.Native ??= ((SKImage) symbol.Icon.Atlas.Native).Subset(new SKRectI(symbol.Icon.X, symbol.Icon.Y,
            symbol.Icon.X + symbol.Icon.Width, symbol.Icon.Y + symbol.Icon.Height));

        canvas.DrawImage((SKImage)symbol.Icon.Native, new SKRect(symbol.Padding, symbol.Padding, symbol.Icon.Width * symbol.Scale + symbol.Padding, symbol.Icon.Height * symbol.Scale + symbol.Padding), paint);

        canvas.Restore();
    }

    private static Envelope CreateEnvelope(SKCanvas canvas, EvaluationContext context, IconLineSymbol symbol, double screenX, double screenY)
    {
        if (symbol.Icon == null)
        {
            return new Envelope();
        }

        var width = symbol.Icon.Width * symbol.Scale + symbol.Padding * 2;
        var height = symbol.Icon.Height * symbol.Scale + symbol.Padding * 2;
        var anchor = new Point(symbol.Anchor.X * width, symbol.Anchor.Y * height);
        var offset = new Point(anchor.X + symbol.Offset.X, anchor.Y + symbol.Offset.Y);

        // We now could calc the rough envelope of icon
        var envelope = new Envelope(0 + offset.X, width + offset.X, 0 + offset.Y, height + offset.Y);

        if (symbol.Rotation != 0.0)
        {
            envelope.RotateDegrees(symbol.Rotation);
        }

        envelope.Translate(screenX, screenY);

        if (symbol.Translate != null)
        {
            var translate = symbol.Translate?.Invoke(context) ?? new Point(0, 0);
            var translateAnchor = symbol.TranslateAnchor?.Invoke(context) ?? MapAlignment.Map;

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

    private static bool CheckForSingleSpace(SKCanvas canvas, EvaluationContext context, IconLineSymbol symbol, Quadtree<ISymbol> tree, double screenX, double screenY, double rotation, bool showUnvalidBorders)
    {

        symbol.Envelope = CreateEnvelope(canvas, context, symbol, screenX, screenY);

        var symbols = tree.Query(symbol.Envelope);

        foreach (var other in symbols)
        {
            if (other is not Symbol otherSymbol)
            {
                continue;
            }
            
            if (otherSymbol.Envelope == null)
            {
                // Should not happen
                continue;
            }

            if (!symbol.Envelope.Intersects(otherSymbol.Envelope))
            {
                continue;
            }

            if (symbol.AllowOthers)
            {
                continue;
            }
            
            if (showUnvalidBorders && symbol.Name != otherSymbol.Name)
            {
                canvas.DrawRect(new Envelope((float)symbol.Envelope.MinX, (float)symbol.Envelope.MaxY, (float)symbol.Envelope.MaxX, (float)symbol.Envelope.MinY).ToSKRect(), DebugPaint);
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// Transfer the world coordinates of geometry to screen coordinates
    /// </summary>
    /// <param name="symbol">Symbol with geometry to convert</param>
    /// <param name="worldToScreenConverter">Converter from world to screen coordinates</param>
    /// <returns>Path with screen coordinates</returns>
    private static SKPath CreateScreenPath(IconLineSymbol symbol, Func<double, double, (double, double)> worldToScreenConverter)
    {
        var path = new SKPath();

        var (nextPointX, nextPointY) = worldToScreenConverter(symbol.Geometry.Coordinates[0].X, symbol.Geometry.Coordinates[0].Y);

        path.MoveTo((float)nextPointX, (float)nextPointY);

        for (var i = 1; i < symbol.Geometry.Coordinates.Length; i++)
        {
            (nextPointX, nextPointY) = worldToScreenConverter(symbol.Geometry.Coordinates[i].X, symbol.Geometry.Coordinates[i].Y);

            path.LineTo((float)nextPointX, (float)nextPointY);
        }

        return path;
    }
}