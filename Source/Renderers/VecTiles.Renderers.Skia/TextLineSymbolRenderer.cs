using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using Topten.RichTextKit;
using VecTiles.Common.Enums;
using VecTiles.Common.Exceptions;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Renderers.Skia.Primitives;
using VecTiles.Styles.OpenMapTiles.Extensions;
using TextDirection = Topten.RichTextKit.TextDirection;

namespace VecTiles.Renderers.Skia;

public class TextLineSymbolRenderer : ISymbolRenderer
{
    private static readonly Dictionary<string, Topten.RichTextKit.Style> TextStyles = new();
    private static readonly SKPaint DebugPaint = new SKPaint { Color = SKColors.Green, StrokeWidth = 1, IsStroke = true };

    public static bool CheckForSpace(SKCanvas canvas, EvaluationContext context, ISymbol sym, Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter, bool showValidBorders = false, bool showUnvalidBorders = false)
    {
        if (sym is not TextLineSymbol symbol)
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
        if (sym is not TextLineSymbol symbol)
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

            DrawText(canvas, context, symbol, nextPosition.X, nextPosition.Y, rotation);

            tree.Insert(symbol.Envelope, symbol);
        }
    }

    private static void DrawText(SKCanvas canvas, EvaluationContext context, TextLineSymbol symbol, double screenX, double screenY, double rotation)
    {
        SKPaint paint = new SKPaint();

        canvas.Save();

        canvas.Translate((float)screenX, (float)screenY);

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

        //canvas.Translate((float)(symbol.Anchor.X * symbol.Icon.Width * symbol.Scale - symbol.Padding), (float)(symbol.Anchor.Y * symbol.Icon.Height * symbol.Scale - symbol.Padding));

        //canvas.DrawImage((SKImage)symbol.Icon.Native, new SKRect(symbol.Padding, symbol.Padding, symbol.Icon.Width * symbol.Scale + symbol.Padding, symbol.Icon.Height * symbol.Scale + symbol.Padding), paint);

        canvas.Restore();
    }

    private static Envelope CreateEnvelope(SKCanvas canvas, EvaluationContext context, TextLineSymbol symbol, double screenX, double screenY)
    {
        if (symbol.Text == null)
        {
            return new Envelope();
        }

        symbol.Renderer ??= new RenderedText(symbol.Text, CreateFonts(symbol.FontNames));
        
        var text = (RenderedText) symbol.Renderer;

        text.TextStyle.FontSize = symbol.FontSize.Invoke(context);
        
        // Set max width to the correct value
        text.Text.MaxWidth = symbol.MaxWidth.Invoke(context, text.TextStyle.FontSize);
        text.Text.ApplyStyle(0, text.Text.Length, text.TextStyle);

        // MeasuredWidth and MeasuredHeight need many CPU cycles, so save them for later use
        text.MeasuredWidth = text.Text.MeasuredWidth;
        text.MeasuredHeight = text.Text.MeasuredHeight;
        // MaxWidth could be greater than MeasuredWidth. Then there is some space around the text.
        // To save another call to MeasuredWidth (with another time-consuming layout), we save the 
        // amount of space in front of the text and remove this when drawing the text.
        text.LeftRightCorrection = (float)(text.Text.MaxWidth - text.MeasuredWidth) * symbol.Alignment switch
        {
            TextJustify.Left => 0f,
            TextJustify.Center => 0.5f,
            TextJustify.Right => 1.0f,
            TextJustify.Auto => text.Text.BaseDirection switch
            {
                TextDirection.LTR => 0.0f,
                TextDirection.RTL => 1.0f,
                _ => 0.0f  // TODO: Not sure what happens with TextDirection.Auto
            },
            _ => 0.0f
        };

        var anchor = new SKPoint((float)(symbol.Anchor.X * text.MeasuredWidth), (float)(symbol.Anchor.Y * text.MeasuredHeight));
        var offset = new SKPoint((float)(anchor.X + symbol.Offset.X), (float)(anchor.Y + symbol.Offset.Y));

        // We now could calc the rough envelope of text
        var envelope = new Envelope(0 + offset.X, text.MeasuredWidth + offset.X, 0 + offset.Y, text.MeasuredHeight + offset.Y);

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

    private static bool CheckForSingleSpace(SKCanvas canvas, EvaluationContext context, TextLineSymbol symbol, Quadtree<ISymbol> tree, double screenX, double screenY, double rotation, bool showUnvalidBorders)
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
    private static SKPath CreateScreenPath(TextLineSymbol symbol, Func<double, double, (double, double)> worldToScreenConverter)
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
    
    private static Style? CreateFonts(string[] names)
    {
        if (names == null || names.Length == 0)
        {
            throw new FontNotFoundException($"Font of style not found");
        }

        foreach (var name in names)
        {
            var textStyle = CreateFont(name);

            if (textStyle != null)
            {
                return textStyle;
            }
        }
        
        throw new FontNotFoundException(names.Length == 1 ? $"Font '{names[0]}' not found" : $"Fonts '{string.Join(", ", names)}' not found");
    }
    
    private static Topten.RichTextKit.Style? CreateFont(string fontName)
    {
        if (string.IsNullOrEmpty(fontName))
        {
            return null;
        }

        if (TextStyles.TryGetValue(fontName, out var textStyle))
        {
            return textStyle;
        }

        textStyle = new Topten.RichTextKit.Style();

        // TODO: Create correct family name
        var fontFamilyName = fontName;

        if (fontFamilyName.Contains("condensed", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontWidth = SKFontStyleWidth.Condensed;
            fontFamilyName = fontFamilyName.Replace("condensed", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        textStyle.FontWeight = 400;

        if (fontFamilyName.Contains("regular", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontWeight = 400;
            fontFamilyName = fontFamilyName.Replace("regular", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        if (fontFamilyName.Contains("medium", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontWeight = 500;
            fontFamilyName = fontFamilyName.Replace("medium", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        if (fontFamilyName.Contains("bold", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontWeight = 500;
            fontFamilyName = fontFamilyName.Replace("bold", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        if (fontFamilyName.Contains("italic", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontItalic = true;
            fontFamilyName = fontFamilyName.Replace("italic", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        fontFamilyName = fontFamilyName.Replace("  ", " ").Trim();

        var fontManager = SKFontManager.Default;
        var typeface = fontManager.MatchFamily(fontFamilyName);

        // Check, if result is the font family asked for
        bool fontExists = typeface != null && typeface.FamilyName.Equals(fontFamilyName, StringComparison.OrdinalIgnoreCase);

        if (!fontExists)
        {
            return null;
        }

        textStyle.FontFamily = fontFamilyName;

        TextStyles.Add(fontName, textStyle);

        return textStyle;
    }
}