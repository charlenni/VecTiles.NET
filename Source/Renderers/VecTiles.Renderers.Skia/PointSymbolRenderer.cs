using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Styles.OpenMapTiles;

namespace VecTiles.Renderers.Skia;

public class PointSymbolRenderer : ISymbolRenderer
{
    public static bool CheckForSpace(SKCanvas canvas, EvaluationContext context, ISymbol sym, Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter, bool showValidBorders = false, bool showUnvalidBorders = false)
    {
        if (sym is not PointSymbol symbol)
        {
            return false;
        }
        
        bool spaceForIconAvailable = IconPointSymbolRenderer.CheckForSpace(canvas, context, symbol.IconSymbol, tree, worldToScreenConverter, showValidBorders, showUnvalidBorders);
        bool spaceForTextAvailable = TextPointSymbolRenderer.CheckForSpace(canvas, context, symbol.TextSymbol, tree, worldToScreenConverter, showValidBorders, showUnvalidBorders);
        symbol.SetDrawFlags(symbol.HasIcon && spaceForIconAvailable && (spaceForTextAvailable || symbol.DrawIconWithoutText),
                            symbol.HasText && spaceForTextAvailable && (spaceForIconAvailable || symbol.DrawTextWithoutIcon));

        return symbol.DrawIcon | symbol.DrawText;
    }

    public static void Draw(SKCanvas canvas, EvaluationContext context, ISymbol sym, ref Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter)
    {
        if (sym is not PointSymbol symbol)
        {
            return;
        }

        if (symbol is { DrawIcon: true, IconSymbol: not null })
        {
            IconPointSymbolRenderer.Draw(canvas, context, symbol.IconSymbol, ref tree, worldToScreenConverter);
        }

        if (symbol is { DrawText: true, TextSymbol: not null })
        {
            TextPointSymbolRenderer.Draw(canvas, context, symbol.TextSymbol, ref tree, worldToScreenConverter);
        }
    }
}