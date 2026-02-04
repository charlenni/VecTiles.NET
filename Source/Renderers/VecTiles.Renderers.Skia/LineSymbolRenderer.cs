using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Styles.OpenMapTiles;

namespace VecTiles.Renderers.Skia;

public class LineSymbolRenderer : ISymbolRenderer
{
    public static bool CheckForSpace(SKCanvas canvas, EvaluationContext context, ISymbol sym, Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter, bool showUnvalidBorders = false)
    {
        if (sym is not LineSymbol symbol)
        {
            return false;
        }
        
        bool spaceForIconAvailable = IconLineSymbolRenderer.CheckForSpace(canvas, context, symbol.IconSymbol, tree, worldToScreenConverter, showUnvalidBorders);
        bool spaceForTextAvailable = TextLineSymbolRenderer.CheckForSpace(canvas, context, symbol.TextSymbol, tree, worldToScreenConverter, showUnvalidBorders);
        symbol.SetDrawFlags(symbol.HasIcon && spaceForIconAvailable && (spaceForTextAvailable || symbol.DrawIconWithoutText),
                            symbol.HasText && spaceForTextAvailable && (spaceForIconAvailable || symbol.DrawTextWithoutIcon));

        return symbol.DrawIcon | symbol.DrawText;
    }

    public static void Draw(SKCanvas canvas, EvaluationContext context, ISymbol sym, ref Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter, bool showValidBorders = false)
    {
        if (sym is not LineSymbol symbol)
        {
            return;
        }

        if (symbol is { DrawIcon: true, IconSymbol: not null })
        {
            IconLineSymbolRenderer.Draw(canvas, context, symbol.IconSymbol, ref tree, worldToScreenConverter, showValidBorders);
        }

        if (symbol is { DrawText: true, TextSymbol: not null })
        {
            TextLineSymbolRenderer.Draw(canvas, context, symbol.TextSymbol, ref tree, worldToScreenConverter, showValidBorders);
        }
    }
}