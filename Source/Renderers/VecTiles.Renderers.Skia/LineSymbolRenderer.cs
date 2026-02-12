using System.Runtime.InteropServices.Marshalling;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Skia;

public class LineSymbolRenderer : ISymbolRenderer
{
    public static bool CheckForSpace(SKCanvas canvas, EvaluationContext context, ISymbol sym, Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter, bool showUnvalidBorders = false)
    {
        if (sym is not LineSymbol symbol)
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

            bool spaceForIconAvailable = false;

            if (symbol.IconSymbol is not null)
            {
                var iconRotation = CalcRotation(symbol.IconSymbol!.Rotation, symbol.IconSymbol!.RotationAlignment, tangent, symbol.IconSymbol.KeepUpright);

                spaceForIconAvailable = IconLineSymbolRenderer.CheckForSingleSpace(canvas, context,
                    symbol.IconSymbol!, tree, nextPosition.X,
                    nextPosition.Y, iconRotation, showUnvalidBorders);
            }

            bool spaceForTextAvailable = false;

            if (symbol.TextSymbol is not null)
            {
                var textRotation = CalcRotation(symbol.TextSymbol!.Rotation, symbol.TextSymbol!.RotationAlignment, tangent, symbol.TextSymbol.KeepUpright);

                spaceForTextAvailable = TextLineSymbolRenderer.CheckForSingleSpace(canvas, context,
                    symbol.TextSymbol!, tree, nextPosition.X,
                    nextPosition.Y, textRotation, showUnvalidBorders);
            }

            if ((symbol.HasIcon && spaceForIconAvailable && (spaceForTextAvailable || symbol.DrawIconWithoutText)) ||
                symbol.HasText && spaceForTextAvailable && (spaceForIconAvailable || symbol.DrawTextWithoutIcon))
            {
                // There is space for one of icon or text at any position of the path
                return true;
            }
        }

        return false;
    }

    public static void Draw(SKCanvas canvas, EvaluationContext context, ISymbol sym, ref Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter, bool showValidBorders = false)
    {
        if (sym is not LineSymbol symbol)
        {
            return;
        }

        var path = CreateScreenPath(symbol, worldToScreenConverter);

        using var pathMeasure = new SKPathMeasure(path);
        
        for (var pos = 0f; pos < pathMeasure.Length; pos = pos + symbol.Spacing)
        {
            var iconDrawn = false;
            var textDrawn = false;
            
            if (pathMeasure.Length < symbol.Spacing)
            {
                // We could only place one symbol in this part, so set it in the middle
                pos = pathMeasure.Length / 2;
            }
            
            pathMeasure.GetPositionAndTangent(pos, out var nextPosition, out var tangentVec);
                
            var tangent = 360f - Math.Atan2(tangentVec.Y, tangentVec.X) * 180 / Math.PI;
            
            var spaceForIconAvailable = false;
            var iconRotation = 0f;

            if (symbol.IconSymbol is not null)
            {
                iconRotation = CalcRotation(symbol.IconSymbol!.Rotation, symbol.IconSymbol!.RotationAlignment, tangent, symbol.IconSymbol.KeepUpright);

                spaceForIconAvailable = IconLineSymbolRenderer.CheckForSingleSpace(canvas, context, symbol.IconSymbol!,
                    tree, nextPosition.X,
                    nextPosition.Y, iconRotation, false);
            }

            var spaceForTextAvailable = false;
            var textRotation = 0f;

            if (symbol.TextSymbol is not null)
            {
                textRotation = CalcRotation(symbol.TextSymbol!.Rotation, symbol.TextSymbol!.RotationAlignment, tangent, symbol.TextSymbol.KeepUpright);

                spaceForTextAvailable = TextLineSymbolRenderer.CheckForSingleSpace(canvas, context,
                    symbol.TextSymbol!, tree, nextPosition.X,
                    nextPosition.Y, textRotation, false);
            }

            if (spaceForIconAvailable && symbol.HasIcon && (spaceForTextAvailable || symbol.DrawIconWithoutText))
            {
                // Draw icon
                IconLineSymbolRenderer.DrawIcon(canvas, context, symbol.IconSymbol!, nextPosition.X, nextPosition.Y, iconRotation, showValidBorders);

                iconDrawn = true;
            }
                
            if (spaceForTextAvailable && symbol.HasText && (spaceForIconAvailable || symbol.DrawTextWithoutIcon))
            {
                TextLineSymbolRenderer.DrawText(canvas, context, symbol.TextSymbol!, nextPosition.X, nextPosition.Y, textRotation, showValidBorders);

                textDrawn = true;
            }

            if (iconDrawn)
            {
                tree.Insert(symbol.IconSymbol!.Envelope, symbol.IconSymbol.Copy());
            }

            if (textDrawn)
            {
                tree.Insert(symbol.TextSymbol!.Envelope, symbol.TextSymbol.Copy());
            }
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
    
    /// <summary>
    /// Transfer the world coordinates of geometry to screen coordinates
    /// </summary>
    /// <param name="symbol">Symbol with geometry to convert</param>
    /// <param name="worldToScreenConverter">Converter from world to screen coordinates</param>
    /// <returns>Path with screen coordinates</returns>
    private static SKPath CreateScreenPath(LineSymbol symbol, Func<double, double, (double, double)> worldToScreenConverter)
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