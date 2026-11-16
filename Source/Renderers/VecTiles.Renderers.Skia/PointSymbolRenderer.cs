using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;

namespace VecTiles.Renderers.Skia;

/// <summary>
/// Renders point symbols (icons and text) on a Skia canvas.
/// </summary>
public class PointSymbolRenderer : ISymbolRenderer
{
    /// <summary>
    /// Checks if there is space to draw the point symbol at its world coordinates.
    /// </summary>
    /// <param name="canvas">The Skia canvas to check against.</param>
    /// <param name="context">The evaluation context for styling.</param>
    /// <param name="sym">The symbol to check.</param>
    /// <param name="tree">The quadtree of existing symbols for collision detection.</param>
    /// <param name="worldToScreenConverter">Function to convert world coordinates to screen coordinates.</param>
    /// <param name="rotation">The rotation angle in degrees.</param>
    /// <param name="showUnvalidBorders">Whether to draw debug borders for collisions.</param>
    /// <returns>A <see cref="SymbolPlacementStatus"/> indicating if the symbol can be placed.</returns>
    public static SymbolPlacementStatus CheckForSpace(SKCanvas canvas, EvaluationContext context, ISymbol sym,
        Quadtree<ISymbol> tree,
        Func<double, double, (double, double)> worldToScreenConverter, double rotation, bool showUnvalidBorders = false)
    {
        if (sym is not PointSymbol symbol)
        {
            return SymbolPlacementStatus.NotInitialized;
        }

        var (screenX, screenY) = worldToScreenConverter(symbol.Point.X, symbol.Point.Y);

        SymbolPlacementStatus spaceForIconAvailable = SymbolPlacementStatus.NotInitialized;
        SymbolPlacementStatus spaceForTextAvailable = SymbolPlacementStatus.NotInitialized;

        if (symbol.IconSymbol is not null)
        {
            spaceForIconAvailable = IconPointSymbolRenderer.CheckForSpace(canvas, context, symbol.IconSymbol, tree,
                screenX, screenY, showUnvalidBorders);
        }

        if (symbol.TextSymbol is not null)
        {
            spaceForTextAvailable = TextPointSymbolRenderer.CheckForSpace(canvas, context, symbol.TextSymbol, tree,
                screenX, screenY, rotation, showUnvalidBorders);
        }

        symbol.SetDrawFlags(
            symbol.HasIcon && spaceForIconAvailable == SymbolPlacementStatus.Success &&
            (spaceForTextAvailable == SymbolPlacementStatus.Success || symbol.DrawIconWithoutText),
            symbol.HasText && spaceForTextAvailable == SymbolPlacementStatus.Success &&
            (spaceForIconAvailable == SymbolPlacementStatus.Success || symbol.DrawTextWithoutIcon));

        return symbol.DrawIcon | symbol.DrawText ? SymbolPlacementStatus.Success : SymbolPlacementStatus.NotInitialized;
    }

    /// <summary>
    /// Draws the point symbol (icon and/or text) on the specified canvas.
    /// </summary>
    /// <param name="canvas">The Skia canvas to draw on.</param>
    /// <param name="context">The evaluation context for styling.</param>
    /// <param name="sym">The symbol to draw.</param>
    /// <param name="tree">The quadtree to track drawn symbols.</param>
    /// <param name="worldToScreenConverter">Function to convert world coordinates to screen coordinates.</param>
    /// <param name="rotation">The rotation angle in degrees.</param>
    /// <param name="showValidBorders">Whether to draw debug borders around drawn symbols.</param>
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

        var spaceForIconAvailable = SymbolPlacementStatus.NotInitialized;
        var iconRotation = 0f;

        if (symbol.IconSymbol is not null)
        {
            iconRotation = CalcRotation(symbol.IconSymbol!.Rotation, symbol.IconSymbol!.RotationAlignment,
                rotation, symbol.IconSymbol.KeepUpright);

            spaceForIconAvailable = IconPointSymbolRenderer.CheckForSpace(canvas, context, symbol.IconSymbol!,
                tree, screenX, screenY, false);
        }

        var spaceForTextAvailable = SymbolPlacementStatus.NotInitialized;
        var textRotation = 0f;

        if (symbol.TextSymbol is not null)
        {
            textRotation = CalcRotation(symbol.TextSymbol!.Rotation, symbol.TextSymbol!.RotationAlignment,
                rotation, symbol.TextSymbol.KeepUpright);

            spaceForTextAvailable = TextPointSymbolRenderer.CheckForSpace(canvas, context, symbol.TextSymbol!,
                tree, screenX, screenY, textRotation, false);
        }

        if (spaceForIconAvailable == SymbolPlacementStatus.Success && symbol.HasIcon &&
            (spaceForTextAvailable == SymbolPlacementStatus.Success || symbol.DrawIconWithoutText))
        {
            // Draw icon
            IconPointSymbolRenderer.DrawIcon(canvas, context, symbol.IconSymbol!, screenX, screenY, showValidBorders);

            iconDrawn = true;
        }

        if (spaceForTextAvailable == SymbolPlacementStatus.Success && symbol.HasText &&
            (spaceForIconAvailable == SymbolPlacementStatus.Success || symbol.DrawTextWithoutIcon))
        {
            TextPointSymbolRenderer.DrawText(canvas, context, symbol.TextSymbol!, screenX, screenY,
                textRotation, showValidBorders);

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

    /// <summary>
    /// Calculates the final rotation for a symbol based on its own rotation, alignment, and map rotation.
    /// </summary>
    /// <param name="rotation">The symbol's base rotation.</param>
    /// <param name="alignment">The rotation alignment (Map or Viewport).</param>
    /// <param name="tangent">The tangent angle of the path or map rotation.</param>
    /// <param name="keepUpright">Whether to keep the symbol upright.</param>
    /// <returns>The final rotation in degrees.</returns>
    private static float CalcRotation(float rotation, MapAlignment alignment, double tangent, bool keepUpright)
    {
        var result = -rotation;
        result -= (float)(alignment is MapAlignment.Map or MapAlignment.Auto ? tangent : 0.0);
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
}
