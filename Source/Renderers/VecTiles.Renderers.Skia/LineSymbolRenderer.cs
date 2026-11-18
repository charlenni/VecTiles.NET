using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Primitives;

namespace VecTiles.Renderers.Skia;

/// <summary>
/// Renders line symbols (icons and text) on a Skia canvas.
/// </summary>
public class LineSymbolRenderer : ISymbolRenderer
{
    /// <summary>
    /// Checks if there is space to draw the line symbol along its path.
    /// All contours of the (possibly merged) geometry are checked, so that a
    /// symbol with space on any contour can be placed.
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
        if (sym is not LineSymbol symbol)
        {
            return SymbolPlacementStatus.NotInitialized;
        }

        foreach (var contour in CreateContourPaths(symbol, worldToScreenConverter))
        {
            using (contour)
            using (var pathMeasure = new SKPathMeasure(contour))
            {
                for (var pos = symbol.Spacing / 4f; pos < pathMeasure.Length; pos = pos + symbol.Spacing)
                {
                    if (pathMeasure.Length < symbol.Spacing)
                    {
                        // We can only place one symbol in this part, so set it in the middle
                        pos = pathMeasure.Length / 2;
                    }

                    pathMeasure.GetPositionAndTangent(pos, out var nextPosition, out var tangentVec);

                    var tangent = 360f - Math.Atan2(tangentVec.Y, tangentVec.X) * 180 / Math.PI;

                    var spaceForIconAvailable = SymbolPlacementStatus.NotInitialized;

                    if (symbol.IconSymbol is not null)
                    {
                        spaceForIconAvailable = IconLineSymbolRenderer.CheckForSingleSpace(canvas, context,
                            contour, pos,
                            symbol.IconSymbol!,
                            tree, nextPosition.X,
                            nextPosition.Y, showUnvalidBorders);
                    }

                    var spaceForTextAvailable = SymbolPlacementStatus.NotInitialized;

                    if (symbol.TextSymbol is not null)
                    {
                        spaceForTextAvailable = TextLineSymbolRenderer.CheckForSingleSpace(canvas, context,
                            contour, pos,
                            symbol.TextSymbol!,
                            tree, showUnvalidBorders, out _);
                    }

                    if ((symbol.HasIcon && spaceForIconAvailable == SymbolPlacementStatus.Success &&
                        (spaceForTextAvailable == SymbolPlacementStatus.Success || symbol.DrawIconWithoutText)) ||
                        symbol.HasText && spaceForTextAvailable == SymbolPlacementStatus.Success &&
                        (spaceForIconAvailable == SymbolPlacementStatus.Success || symbol.DrawTextWithoutIcon))
                    {
                        // There is space for at least one of icon or text at any position of the path
                        return SymbolPlacementStatus.Success;
                    }
                }
            }
        }

        return SymbolPlacementStatus.Collision;
    }

    /// <summary>
    /// Draws the line symbol (icons and/or text) along its path on the specified canvas.
    /// All contours of the (possibly merged) geometry are drawn.
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
        if (sym is not LineSymbol symbol)
        {
            return;
        }

        foreach (var contour in CreateContourPaths(symbol, worldToScreenConverter))
        {
            using (contour)
            using (var pathMeasure = new SKPathMeasure(contour))
            {
                for (var pos = symbol.Spacing / 4f; pos < pathMeasure.Length; pos = pos + symbol.Spacing)
                {
                    var iconDrawn = false;
                    var textDrawn = false;

                    if (pathMeasure.Length < symbol.Spacing)
                    {
                        // We can only place one symbol in this part, so set it in the middle
                        pos = pathMeasure.Length / 2;
                    }

                    pathMeasure.GetPositionAndTangent(pos, out var nextPosition, out var tangentVec);

                    var tangent = 360f - Math.Atan2(tangentVec.Y, tangentVec.X) * 180 / Math.PI;

                    var spaceForIconAvailable = SymbolPlacementStatus.NotInitialized;
                    var iconRotation = 0f;

                    if (symbol.IconSymbol is not null)
                    {
                        iconRotation = CalcRotation(symbol.IconSymbol!.Rotation, symbol.IconSymbol!.RotationAlignment,
                            tangent, symbol.IconSymbol.KeepUpright);

                        spaceForIconAvailable = IconLineSymbolRenderer.CheckForSingleSpace(canvas, context,
                            contour, pos,
                            symbol.IconSymbol!,
                            tree, nextPosition.X,
                            nextPosition.Y, false);
                    }

                    var spaceForTextAvailable = SymbolPlacementStatus.NotInitialized;
                    PathTextPlacement? textPlacement = null;

                    if (symbol.TextSymbol is not null)
                    {
                        spaceForTextAvailable = TextLineSymbolRenderer.CheckForSingleSpace(canvas, context,
                            contour, pos,
                            symbol.TextSymbol!, tree, false, out textPlacement);
                    }

                    if (symbol.HasIcon && spaceForIconAvailable == SymbolPlacementStatus.Success &&
                        (spaceForTextAvailable == SymbolPlacementStatus.Success || symbol.DrawIconWithoutText))
                    {
                        // Draw icon
                        IconLineSymbolRenderer.DrawIcon(canvas, context, symbol.IconSymbol!, nextPosition.X,
                            nextPosition.Y, iconRotation, showValidBorders);

                        iconDrawn = true;
                    }

                    if (symbol.HasText && spaceForTextAvailable == SymbolPlacementStatus.Success &&
                        (spaceForIconAvailable == SymbolPlacementStatus.Success || symbol.DrawTextWithoutIcon))
                    {
                        TextLineSymbolRenderer.DrawTextOnPath(canvas, context, symbol.TextSymbol!, contour,
                            textPlacement!, showValidBorders);

                        if (!symbol.HasIcon)
                        {
                            pos += ((RenderedText)symbol.TextSymbol.Native).MeasuredWidth;
                        }

                        textDrawn = true;
                    }

                    if (iconDrawn)
                    {
                        tree.Insert(symbol.IconSymbol!.ScreenEnvelope, symbol.IconSymbol.Copy());
                    }

                    if (textDrawn)
                    {
                        // Insert one copy per collision box of the label, so other
                        // symbols collide with the label along the whole path
                        foreach (var box in textPlacement!.Boxes)
                        {
                            var copy = symbol.TextSymbol!.Copy();
                            copy.ScreenEnvelope = box;

                            tree.Insert(box, copy);
                        }
                    }
                }
            }
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

    /// <summary>
    /// Converts the world coordinates of a geometry to screen coordinates and
    /// creates one path per contour of the geometry. Checking and drawing run
    /// per contour, so that labels of other contours do not influence the
    /// measured length of the current one.
    /// </summary>
    /// <param name="symbol">Symbol with geometry to convert.</param>
    /// <param name="worldToScreenConverter">Converter from world to screen coordinates.</param>
    /// <returns>Paths with screen coordinates, one per contour.</returns>
    private static List<SKPath> CreateContourPaths(LineSymbol symbol,
        Func<double, double, (double, double)> worldToScreenConverter)
    {
        var paths = new List<SKPath>();

        // A merged symbol can carry a MultiLineString when segments of the
        // same feature are not directly connected
        IEnumerable<LineString> parts = symbol.Geometry switch
        {
            MultiLineString multi => multi.Geometries.OfType<LineString>(),
            LineString single => [single],
            _ => Enumerable.Empty<LineString>(),
        };

        foreach (var part in parts)
        {
            if (part.Coordinates.Length < 2)
            {
                continue;
            }

            var path = new SKPath();

            var (nextPointX, nextPointY) = worldToScreenConverter(part.Coordinates[0].X,
                part.Coordinates[0].Y);

            path.MoveTo((float)nextPointX, (float)nextPointY);

            for (var i = 1; i < part.Coordinates.Length; i++)
            {
                (nextPointX, nextPointY) = worldToScreenConverter(part.Coordinates[i].X,
                    part.Coordinates[i].Y);

                path.LineTo((float)nextPointX, (float)nextPointY);
            }

            paths.Add(path);
        }

        return paths;
    }
}
