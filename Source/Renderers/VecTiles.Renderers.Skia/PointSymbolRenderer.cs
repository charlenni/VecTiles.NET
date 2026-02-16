using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Skia;

public class PointSymbolRenderer : ISymbolRenderer
{
    public static bool CheckForSpace(SKCanvas canvas, EvaluationContext context, ISymbol sym, Quadtree<ISymbol> tree, 
        Func<double, double, (double, double)> worldToScreenConverter, double rotation, bool showUnvalidBorders = false)
    {
        if (sym is not PointSymbol symbol)
        {
            return false;
        }

        var (screenX, screenY) = worldToScreenConverter(symbol.Point.X, symbol.Point.Y);

        bool spaceForIconAvailable = false;
        bool spaceForTextAvailable = false;

        if (symbol.IconSymbol is not null)
        {
            spaceForIconAvailable = IconPointSymbolRenderer.CheckForSpace(canvas, context, symbol.IconSymbol, tree, screenX, screenY, rotation, showUnvalidBorders);
        }

        if (symbol.TextSymbol is not null)
        {
            spaceForTextAvailable = TextPointSymbolRenderer.CheckForSpace(canvas, context, symbol.TextSymbol, tree,
                screenX, screenY, rotation, showUnvalidBorders);
        }

        symbol.SetDrawFlags(symbol.HasIcon && spaceForIconAvailable && (spaceForTextAvailable || symbol.DrawIconWithoutText),
                            symbol.HasText && spaceForTextAvailable && (spaceForIconAvailable || symbol.DrawTextWithoutIcon));

        return symbol.DrawIcon | symbol.DrawText;
    }

    public static void Draw(SKCanvas canvas, EvaluationContext context, ISymbol sym, ref Quadtree<ISymbol> tree, 
        Func<double, double, (double, double)> worldToScreenConverter, double rotation, bool showValidBorders = false)
    {
        var iconDrawn = false;
        var textDrawn = false;

        if (sym is not PointSymbol symbol)
        {
            return;
        }

        var (screenX, screenY) = worldToScreenConverter(symbol.Point.X, symbol.Point.Y);
        
        var spaceForIconAvailable = false;
        var iconRotation = 0f;

        if (symbol.IconSymbol is not null)
        {
            iconRotation = CalcRotation(symbol.IconSymbol!.Rotation, symbol.IconSymbol!.RotationAlignment, rotation, symbol.IconSymbol.KeepUpright);

            spaceForIconAvailable = IconPointSymbolRenderer.CheckForSpace(canvas, context, symbol.IconSymbol!, tree, screenX, screenY, iconRotation, false);
        }

        var spaceForTextAvailable = false;
        var textRotation = 0f;

        if (symbol.TextSymbol is not null)
        {
            textRotation = CalcRotation(symbol.TextSymbol!.Rotation, symbol.TextSymbol!.RotationAlignment, rotation, symbol.TextSymbol.KeepUpright);

            spaceForTextAvailable = TextPointSymbolRenderer.CheckForSpace(canvas, context, symbol.TextSymbol!, tree, screenX, screenY, textRotation, false);
        }

        if (spaceForIconAvailable && symbol.HasIcon && (spaceForTextAvailable || symbol.DrawIconWithoutText))
        {
            // Draw icon
            IconPointSymbolRenderer.DrawIcon(canvas, context, symbol.IconSymbol!, screenX, screenY, iconRotation, showValidBorders);

            iconDrawn = true;
        }
                
        if (spaceForTextAvailable && symbol.HasText && (spaceForIconAvailable || symbol.DrawTextWithoutIcon))
        {
            TextPointSymbolRenderer.DrawText(canvas, context, symbol.TextSymbol!, screenX, screenY, textRotation, showValidBorders);

            textDrawn = true;
        }

        if (iconDrawn)
        {
            tree.Insert(symbol.IconSymbol!.ScreenEnvelope, symbol.IconSymbol.Copy());
        }

        if (textDrawn)
        {
            tree.Insert(symbol.TextSymbol!.ScreenEnvelope, symbol.TextSymbol.Copy());
        }
    }

    private static float CalcRotation(float rotation, MapAlignment alignment, double tangent, bool keepUpright)
    {
        var result = -rotation;
        result -= (float) (alignment is MapAlignment.Map or MapAlignment.Auto ? tangent : 0.0);
        result %= 360;

        if (keepUpright && result is > 90 and < 270)
        {
            result -= 180;
        }

        if (keepUpright && result is < -90 and > -270)
        {
            result += 180;
        }

        return result;
    }

    /*
        
        
        
        if (symbol is { DrawIcon: true, IconSymbol: not null })
        {
            IconPointSymbolRenderer.Draw(canvas, context, symbol.IconSymbol, ref tree, worldToScreenConverter, showValidBorders);
        }

        if (symbol is { DrawText: true, TextSymbol: not null })
        {
            TextPointSymbolRenderer.DrawText(canvas, context, symbol.TextSymbol, screenX, screenY, 0, showValidBorders);
        }
    }*/
}