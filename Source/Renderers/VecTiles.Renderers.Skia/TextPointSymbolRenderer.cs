using System.Net.Mime;
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

public class TextPointSymbolRenderer : ISymbolRenderer
{
    private static readonly Dictionary<string, Topten.RichTextKit.Style> TextStyles = new();
    private static readonly SKPaint DebugPaint = new SKPaint { Color = SKColors.Blue, StrokeWidth = 1, IsStroke = true };

    public static bool CheckForSpace(SKCanvas canvas, EvaluationContext context, ISymbol sym, Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter, bool showValidBorders = false, bool showInvalidBorders = false)
    {
        if (sym is not TextPointSymbol symbol)
        {
            return false;
        }

        var (screenX, screenY) = worldToScreenConverter(symbol.Point.X, symbol.Point.Y);

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

            if (!symbol.AllowOthers)
            {
                if (showInvalidBorders && symbol.Name != otherSymbol.Name)
                {
                    canvas.DrawRect(new SKRect((float)symbol.Envelope.MinX, (float)symbol.Envelope.MaxY, (float)symbol.Envelope.MaxX, (float)symbol.Envelope.MinY), DebugPaint);
                }

                return false;
            }
        }

        if (showValidBorders)
        {
            canvas.DrawRect(new SKRect((float)symbol.Envelope.MinX, (float)symbol.Envelope.MaxY, (float)symbol.Envelope.MaxX, (float)symbol.Envelope.MinY), DebugPaint);
        }

        return true;
    }

    public static void Draw(SKCanvas canvas, EvaluationContext context, ISymbol sym, ref Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter)
    {
        if (sym is not TextPointSymbol symbol)
        {
            return;
        }

        //var envelope = CreateEnvelope(canvas, context, screenX, screenY);
        var (screenX, screenY) = worldToScreenConverter(symbol.Point.X, symbol.Point.Y);

        if (!symbol.Envelope.IsNull)
        {
            DrawText(canvas, context, symbol, screenX, screenY);

            tree.Insert(symbol.Envelope, symbol);
        }
    }

    private static void DrawText(SKCanvas canvas, EvaluationContext context, TextPointSymbol symbol, double screenX, double screenY)
    {
        canvas.Save();

        canvas.Translate((float)screenX, (float)screenY);

        if (symbol.Translate != null)
        {
            var translate = symbol.Translate?.Invoke(context).ToSKPoint() ?? new SKPoint(0, 0);
            var translateAnchor = symbol.TranslateAnchor?.Invoke(context) ?? MapAlignment.Map;

            if (translateAnchor == MapAlignment.Viewport)
            {
                canvas.RotateDegrees(-context.Rotation);
            }

            canvas.Translate(translate);
        }

        canvas.Scale(1f / context.Scale);
        canvas.Translate(symbol.Offset.ToSKPoint());
        canvas.RotateDegrees(symbol.Rotation);

        var text = (RenderedText) symbol.Renderer;
        
        var textStyle = (Style)text.Text.GetStyleAtOffset(0);

        textStyle.TextColor = symbol.Color.Invoke(context).ToSKColor().WithAlpha((byte)(symbol.Opacity.Invoke(context) * 255f));
        textStyle.HaloBlur = symbol.HaloBlur.Invoke(context);
        textStyle.HaloColor = symbol.HaloColor.Invoke(context).ToSKColor();
        textStyle.HaloWidth = symbol.HaloWidth.Invoke(context);

        text.Text.ApplyStyle(0, text.Text.Length, textStyle);

        // Translate to right position respecting the correction (leading space before TextBlocks text starts)
        canvas.Translate((float)(symbol.Anchor.X * text.MeasuredWidth - text.LeftRightCorrection), (float)(symbol.Anchor.Y * text.MeasuredHeight));

        text.Text.Paint(canvas);

        canvas.Restore();
    }

    private static Envelope CreateEnvelope(SKCanvas canvas, EvaluationContext context, TextPointSymbol symbol, double screenX, double screenY)
    {
        if (string.IsNullOrEmpty(symbol.Text))
        {
            return new Envelope();
        }

        symbol.Renderer ??= new RenderedText(symbol.Text);
        
        var text = (RenderedText) symbol.Renderer;

        if (text.LastContext != null 
            && context.Zoom == text.LastContext.Zoom 
            && context.Scale == text.LastContext.Scale 
            && context.Rotation == text.LastContext.Rotation)
        {
            // Nothing changed, so return the last envelope
            return text.LastEnvelope;
        }

        // Set max width to the correct value
        text.Text.MaxWidth = symbol.MaxWidth.Invoke(context, text.TextStyle.FontSize);
        text.Text.ApplyStyle(0, text.Text.Length, text.TextStyle);

        // MeasuredWidth and MeasuredHeight need many CPU cycles, so save them for later use
        text.MeasuredWidth = text.MeasuredWidth;
        text.MeasuredHeight = text.MeasuredHeight;
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
            var translate = symbol.Translate?.Invoke(context).ToSKPoint() ?? new SKPoint(0, 0);
            var translateAnchor = symbol.TranslateAnchor?.Invoke(context) ?? MapAlignment.Map;

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

        text.LastContext = context;
        text.LastEnvelope = envelope;

        return envelope;
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