using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using Topten.RichTextKit;
using VecTiles.Common.Enums;
using VecTiles.Common.Exceptions;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Renderers.Skia.Primitives;
using VecTiles.Styles.OpenMapTiles.Extensions;

namespace VecTiles.Renderers.Skia;

public static class TextLineSymbolRenderer
{
    private static readonly Dictionary<string, Topten.RichTextKit.Style> TextStyles = new();
    private static readonly SKPaint DebugPaint = new SKPaint { Color = SKColors.Red, StrokeWidth = 1, IsStroke = true };

    public static void DrawText(SKCanvas canvas, EvaluationContext context, TextLineSymbol symbol, double screenX, double screenY, double rotation, bool showValidBorders = false)
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

        var text = (RenderedText) symbol.Renderer;

        text.Text.Alignment = symbol.Alignment.ToTextAlignment(); 
        
        var textStyle = (Style)text.Text.GetStyleAtOffset(0);

        textStyle.TextColor = symbol.Color.Invoke(context).ToSKColor().WithAlpha((byte)(symbol.Opacity.Invoke(context) * 255f));
        textStyle.HaloBlur = symbol.HaloBlur.Invoke(context);
        textStyle.HaloColor = symbol.HaloColor.Invoke(context).ToSKColor();
        textStyle.HaloWidth = symbol.HaloWidth.Invoke(context);

        text.Text.ApplyStyle(0, text.Text.Length, textStyle);

        // Translate to right position respecting the correction (leading space before TextBlocks text starts)
        canvas.Translate((float)(symbol.Anchor.X * text.MeasuredWidth) - text.Text.MeasuredPadding.Left, (float)(symbol.Anchor.Y * text.MeasuredHeight - text.Text.MeasuredPadding.Top));

        text.Text.Paint(canvas);

        canvas.Restore();

        if (showValidBorders)
        {
            canvas.DrawRect(
                new SKRect((float) symbol.Envelope!.MinX, (float) symbol.Envelope!.MinY, (float) symbol.Envelope!.MaxX,
                    (float) symbol.Envelope!.MaxY), DebugPaint);
        }
    }

    public static Envelope CreateEnvelope(SKCanvas canvas, EvaluationContext context, TextLineSymbol symbol, double screenX, double screenY, double rotation)
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
        // This could be access by MeasuredPadding.
        text.LeftRightCorrection = 0f;

        var anchor = new SKPoint((float)(symbol.Anchor.X * text.MeasuredWidth), (float)(symbol.Anchor.Y * text.MeasuredHeight));
        var offset = new SKPoint((float)(anchor.X + symbol.Offset.X), (float)(anchor.Y + symbol.Offset.Y));

        // We now could calc the rough envelope of text
        var envelope = new Envelope(offset.X, offset.X + text.MeasuredWidth, offset.Y, offset.Y + text.MeasuredHeight);

        if (symbol.Rotation != 0.0)
        {
            envelope.RotateDegrees(symbol.Rotation);
        }

        if (rotation != 0.0)
        {
            envelope.RotateDegrees(rotation);
        }

        envelope.Translate(screenX, screenY);

        if (symbol.Translate != null)
        {
            var translate = symbol.Translate?.Invoke(context) ?? new Point(0, 0);
            var translateAnchor = symbol.TranslateAnchor?.Invoke(context) ?? MapAlignment.Map;

            if (translateAnchor == MapAlignment.Map)
            {
                var rot = context.Rotation * Math.PI / 180.0;
                var cos = Math.Cos(rot);
                var sin = Math.Sin(rot);
                var x = translate.X * cos - translate.Y * sin;
                var y = translate.X * sin + translate.Y * cos;
                translate = new Point((float)x, (float)y);
            }

            envelope.Translate(translate.X, translate.Y);
        }

        return envelope;
    }

    public static bool CheckForSingleSpace(SKCanvas canvas, EvaluationContext context, TextLineSymbol symbol, Quadtree<ISymbol> tree, double screenX, double screenY, double rotation, bool showUnvalidBorders)
    {
        symbol.Envelope = CreateEnvelope(canvas, context, symbol, screenX, screenY, rotation);

        if (symbol.Envelope.MaxX < canvas.LocalClipBounds.Left ||
            symbol.Envelope.MinY < canvas.LocalClipBounds.Top ||
            symbol.Envelope.MinX > canvas.LocalClipBounds.Left + canvas.LocalClipBounds.Width ||
            symbol.Envelope.MinY > canvas.LocalClipBounds.Top + canvas.LocalClipBounds.Height)
        {
            // Symbol isn't visible
            return false;
        }
 
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

            if (symbol.AllowOthers  && otherSymbol.AllowOthers)
            {
                continue;
            }
            
            if (showUnvalidBorders && symbol.Name != otherSymbol.Name)
            {
                canvas.DrawRect(new SKRect((float)symbol.Envelope.MinX, (float)symbol.Envelope.MinY, (float)symbol.Envelope.MaxX, (float)symbol.Envelope.MaxY), DebugPaint);
            }

            return false;
        }

        return true;
    }
    
    public static Style? CreateFonts(string[] names)
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