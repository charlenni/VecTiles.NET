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
            using (contour.Path)
            using (var pathMeasure = new SKPathMeasure(contour.Path))
            {
                foreach (var pos in GetAnchorPositions(symbol, pathMeasure.Length, context, contour.Part))
                {
                    pathMeasure.GetPositionAndTangent(pos, out var nextPosition, out var tangentVec);

                    var spaceForIconAvailable = SymbolPlacementStatus.NotInitialized;

                    if (symbol.IconSymbol is not null)
                    {
                        spaceForIconAvailable = IconLineSymbolRenderer.CheckForSingleSpace(canvas, context,
                            contour.Path, pos,
                            symbol.IconSymbol!,
                            tree, nextPosition.X,
                            nextPosition.Y, showUnvalidBorders);
                    }

                    var spaceForTextAvailable = SymbolPlacementStatus.NotInitialized;

                    if (symbol.TextSymbol is not null)
                    {
                        spaceForTextAvailable = TextLineSymbolRenderer.CheckForSingleSpace(canvas, context,
                            contour.Path, pos,
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
    /// Runs the placement of a line symbol against the quadtree and records the
    /// result: every anchor position where icon and/or text find space is
    /// recorded with the exact path placement and the collision boxes of the
    /// label are inserted into the tree. Called by the placement pass before
    /// drawing; <see cref="Draw"/> consumes the returned run without running
    /// the collision checks again.
    /// </summary>
    /// <param name="canvas">Canvas for the collision checks (debug borders only).</param>
    /// <param name="context">The evaluation context for styling.</param>
    /// <param name="sym">The symbol to place.</param>
    /// <param name="tree">The quadtree of already placed symbols; receives the placed labels.</param>
    /// <param name="worldToScreenConverter">Function to convert world coordinates to screen coordinates.</param>
    /// <param name="showUnvalidBorders">Whether to draw debug borders for collisions.</param>
    /// <returns>The placement run with the recorded anchors per geometry contour.</returns>
    public static LineSymbolPlacementRun Place(SKCanvas canvas, EvaluationContext context, ISymbol sym,
        Quadtree<ISymbol> tree, Func<double, double, (double, double)> worldToScreenConverter,
        bool showUnvalidBorders = false)
    {
        if (sym is not LineSymbol symbol)
        {
            return LineSymbolPlacementRun.Empty;
        }

        var contours = CreateContourPaths(symbol, worldToScreenConverter);
        var result = new List<IReadOnlyList<LineSymbolAnchorPlacement>>(contours.Count);

        for (var contourIndex = 0; contourIndex < contours.Count; contourIndex++)
        {
            var (path, part) = contours[contourIndex];
            var anchors = new List<LineSymbolAnchorPlacement>();

            using (path)
            using (var pathMeasure = new SKPathMeasure(path))
            {
                foreach (var pos in GetAnchorPositions(symbol, pathMeasure.Length, context, part))
                {
                    pathMeasure.GetPositionAndTangent(pos, out var nextPosition, out _);

                    var spaceForIconAvailable = SymbolPlacementStatus.NotInitialized;

                    if (symbol.IconSymbol is not null)
                    {
                        spaceForIconAvailable = IconLineSymbolRenderer.CheckForSingleSpace(canvas, context,
                            path, pos,
                            symbol.IconSymbol!,
                            tree, nextPosition.X, nextPosition.Y, showUnvalidBorders);
                    }

                    var spaceForTextAvailable = SymbolPlacementStatus.NotInitialized;
                    PathTextPlacement? textPlacement = null;

                    if (symbol.TextSymbol is not null)
                    {
                        spaceForTextAvailable = TextLineSymbolRenderer.CheckForSingleSpace(canvas, context,
                            path, pos,
                            symbol.TextSymbol!, tree, showUnvalidBorders, out textPlacement);
                    }

                    var iconPlaced = symbol.HasIcon && spaceForIconAvailable == SymbolPlacementStatus.Success &&
                        (spaceForTextAvailable == SymbolPlacementStatus.Success || symbol.DrawIconWithoutText);

                    var textPlaced = symbol.HasText && spaceForTextAvailable == SymbolPlacementStatus.Success &&
                        (spaceForIconAvailable == SymbolPlacementStatus.Success || symbol.DrawTextWithoutIcon);

                    if (!iconPlaced && !textPlaced)
                    {
                        continue;
                    }

                    if (iconPlaced)
                    {
                        tree.Insert(symbol.IconSymbol!.ScreenEnvelope!, symbol.IconSymbol.Copy());
                    }

                    if (textPlaced)
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

                    anchors.Add(new LineSymbolAnchorPlacement(pos, iconPlaced, textPlaced, textPlacement!));
                }
            }

            result.Add(anchors);
        }

        return new LineSymbolPlacementRun(result);
    }

    /// <summary>
    /// Draws the line symbol (icons and/or text) along its path on the specified canvas.
    /// All contours of the (possibly merged) geometry are drawn.
    /// </summary>
    /// <param name="canvas">The Skia canvas to draw on.</param>
    /// <param name="context">The evaluation context for the placement checks.</param>
    /// <param name="sym">The symbol to draw.</param>
    /// <param name="tree">The quadtree to track drawn symbols.</param>
    /// <param name="worldToScreenConverter">Function to convert world coordinates to the screen space of the checks.</param>
    /// <param name="rotation">The rotation angle in degrees.</param>
    /// <param name="showValidBorders">Whether to draw debug borders around drawn symbols.</param>
    /// <param name="drawContext">Optional evaluation context for drawing (style sizes); falls back to <paramref name="context"/>.</param>
    /// <param name="drawWorldToScreenConverter">Optional converter into the screen space used for drawing; falls back to <paramref name="worldToScreenConverter"/>.</param>
    public static void Draw(SKCanvas canvas, EvaluationContext context, ISymbol sym, ref Quadtree<ISymbol> tree,
        Func<double, double, (double, double)> worldToScreenConverter, double rotation, bool showValidBorders = false,
        EvaluationContext? drawContext = null, Func<double, double, (double, double)>? drawWorldToScreenConverter = null,
        LineSymbolPlacementRun? placementRun = null)
    {
        var drawCtx = drawContext ?? context;
        var toDraw = drawWorldToScreenConverter ?? worldToScreenConverter;

        if (sym is not LineSymbol symbol)
        {
            return;
        }

        // Placement runs in the screen space of the checks, drawing in the
        // (differently scaled) real screen space; along-path parameters scale
        // linearly between both spaces
        var contours = CreateContourPaths(symbol, worldToScreenConverter);
        var drawContours = ReferenceEquals(toDraw, worldToScreenConverter)
            ? contours
            : CreateContourPaths(symbol, toDraw);

        for (var contourIndex = 0; contourIndex < contours.Count; contourIndex++)
        {
            var contour = contours[contourIndex].Path;
            var drawContour = drawContours[contourIndex].Path;

            using (contour)
            using (drawContour)
            using (var pathMeasure = new SKPathMeasure(contour))
            using (var drawPathMeasure = new SKPathMeasure(drawContour))
            {
                // Scale factor from the placement space to the draw space
                // (equal vectors of the same polyline, so the ratio is uniform)
                var drawScale = pathMeasure.Length > 0 && drawPathMeasure.Length > 0
                    ? (float)(drawPathMeasure.Length / pathMeasure.Length)
                    : 1f;

                if (placementRun != null)
                {
                    // The placement pass has already decided which anchors and
                    // parts (icon/text) of this contour are drawn; honor the
                    // decisions exactly, run no checks and insert nothing
                    if (contourIndex < placementRun.Contours.Count)
                    {
                        foreach (var placedAnchor in placementRun.Contours[contourIndex])
                        {
                            DrawPlacedAnchor(canvas, context, symbol, drawContour, pathMeasure,
                                drawScale, placedAnchor, showValidBorders, drawCtx);
                        }
                    }

                    continue;
                }

                foreach (var pos in GetAnchorPositions(symbol, pathMeasure.Length, context, contours[contourIndex].Part))
                {
                    var iconDrawn = false;
                    var textDrawn = false;

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
                        // Draw icon; the placement position is converted from
                        // the placement space to the draw space
                        IconLineSymbolRenderer.DrawIcon(canvas, drawCtx, symbol.IconSymbol!, nextPosition.X * drawScale,
                            nextPosition.Y * drawScale, iconRotation, showValidBorders);

                        iconDrawn = true;
                    }

                    if (symbol.HasText && spaceForTextAvailable == SymbolPlacementStatus.Success &&
                        (spaceForIconAvailable == SymbolPlacementStatus.Success || symbol.DrawTextWithoutIcon))
                    {
                        // The glyphs are drawn at the style size of the current
                        // fractional zoom, so the run of the label around the
                        // anchor has the width of the drawn text, not the width
                        // of the canonical placement envelope. The draw start is
                        // shifted accordingly, so the label stays centered on
                        // its anchor while zooming.
                        var textSymbol = symbol.TextSymbol!;
                        var fontSizePlacement = textSymbol.FontSize.Invoke(context);
                        var fontSizeDraw = textSymbol.FontSize.Invoke(drawCtx);
                        var fontSizeRatio = fontSizePlacement > 0 ? fontSizeDraw / fontSizePlacement : 1f;
                        var measuredWidth = ((RenderedText)textSymbol.Native).MeasuredWidth;
                        var leadPlacement = -textSymbol.Anchor.X * measuredWidth + textSymbol.Offset.X;
                        var leadDraw = (float)(-textSymbol.Anchor.X * measuredWidth * fontSizeRatio + textSymbol.Offset.X);

                        // The placement (draw start and path length) is scaled
                        // from the placement space to the draw space, plus the
                        // lead correction of the drawn text size
                        var drawPlacement = textPlacement == null
                            ? null
                            : new PathTextPlacement(textPlacement.Reversed,
                                (float)(textPlacement.DrawStart * drawScale) + leadDraw - (float)(leadPlacement * drawScale),
                                (float)(textPlacement.PathLength * drawScale),
                                textPlacement.Boxes);

                        TextLineSymbolRenderer.DrawTextOnPath(canvas, drawCtx, textSymbol, drawContour,
                            drawPlacement!, showValidBorders, fontSizeRatio);

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
    /// Draws the recorded parts (icon and/or text) of one placed anchor of a
    /// line symbol. Runs no collision checks and inserts nothing into the
    /// tree: the placement pass already decided both.
    /// </summary>
    private static void DrawPlacedAnchor(SKCanvas canvas, EvaluationContext context, LineSymbol symbol,
        SKPath drawContour, SKPathMeasure pathMeasure, float drawScale,
        LineSymbolAnchorPlacement placed, bool showValidBorders, EvaluationContext drawCtx)
    {
        pathMeasure.GetPositionAndTangent(placed.Position, out var nextPosition, out var tangentVec);

        var tangent = 360f - Math.Atan2(tangentVec.Y, tangentVec.X) * 180 / Math.PI;

        if (placed.Icon && symbol.HasIcon)
        {
            var iconRotation = CalcRotation(symbol.IconSymbol!.Rotation, symbol.IconSymbol!.RotationAlignment,
                tangent, symbol.IconSymbol.KeepUpright);

            IconLineSymbolRenderer.DrawIcon(canvas, drawCtx, symbol.IconSymbol!, nextPosition.X * drawScale,
                nextPosition.Y * drawScale, iconRotation, showValidBorders);
        }

        if (placed.Text && symbol.HasText)
        {
            // The glyphs are drawn at the style size of the current
            // fractional zoom, so the run of the label around the
            // anchor has the width of the drawn text, not the width
            // of the canonical placement envelope. The draw start is
            // shifted accordingly, so the label stays centered on
            // its anchor while zooming.
            var textSymbol = symbol.TextSymbol!;
            var fontSizePlacement = textSymbol.FontSize.Invoke(context);
            var fontSizeDraw = textSymbol.FontSize.Invoke(drawCtx);
            var fontSizeRatio = fontSizePlacement > 0 ? fontSizeDraw / fontSizePlacement : 1f;
            var measuredWidth = ((RenderedText)textSymbol.Native).MeasuredWidth;
            var leadPlacement = -textSymbol.Anchor.X * measuredWidth + textSymbol.Offset.X;
            var leadDraw = (float)(-textSymbol.Anchor.X * measuredWidth * fontSizeRatio + textSymbol.Offset.X);

            // The placement (draw start and path length) is scaled
            // from the placement space to the draw space, plus the
            // lead correction of the drawn text size
            var textPlacement = placed.Placement;
            var drawPlacement = textPlacement == null
                ? null
                : new PathTextPlacement(textPlacement.Reversed,
                    (float)(textPlacement.DrawStart * drawScale) + leadDraw - (float)(leadPlacement * drawScale),
                    (float)(textPlacement.PathLength * drawScale),
                    textPlacement.Boxes);

            TextLineSymbolRenderer.DrawTextOnPath(canvas, drawCtx, textSymbol, drawContour,
                drawPlacement!, showValidBorders, fontSizeRatio);
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
    /// Computes the anchor positions of the labels on one contour following the
    /// placement algorithm of MapLibre (get_anchors.ts): the first anchor sits
    /// half a label length plus a fixed extra offset (two em, to avoid
    /// collisions at T intersections) behind the start of the line, so
    /// consecutive labels keep a minimum gap of spacing / 4 between their
    /// edges, and an anchor is only valid when the whole label fits on the
    /// path. Lines continued outside the tile align to the spacing, so labels
    /// of neighboring tiles match. When no anchor is valid, a single label is
    /// centered on the line. Anchors inside the tile rectangle are returned
    /// first, anchors reaching into the tile buffer last.
    /// </summary>
    /// <param name="symbol">Symbol with spacing and label to place.</param>
    /// <param name="pathLength">Length of the contour in the placement space.</param>
    /// <param name="context">Evaluation context for the label size.</param>
    /// <param name="part">Contour geometry in world coordinates.</param>
    /// <returns>Anchor positions along the contour in the placement space.</returns>
    private static List<float> GetAnchorPositions(LineSymbol symbol, float pathLength,
        EvaluationContext context, LineString part)
    {
        var spacing = symbol.Spacing;

        if (spacing <= 0f)
        {
            return [pathLength / 2f];
        }

        var tile = symbol.Tile;
        var isLineContinued = IsLineContinued(part, tile);
        var labelLength = GetLabelLength(symbol, context);

        // Guarantee a minimum gap of spacing / 4 between the edges of adjacent
        // labels (MapLibre widens the spacing for long labels)
        if (spacing - labelLength < spacing / 4f)
        {
            spacing = labelLength + spacing / 4f;
        }

        // MapLibre uses two em of the (max) text size as the fixed extra
        // offset; icon-only symbols fall back to the default text size of the
        // style spec (16 px)
        var fontSize = symbol.TextSymbol?.FontSize.Invoke(context) ?? 16f;

        var offset = (isLineContinued ? spacing / 2f : labelLength / 2f + fontSize * 2f) % spacing;

        var halfLabelLength = labelLength / 2f;

        // Candidate anchors in path order, flagged by whether the anchor point
        // lies inside the tile rectangle. Labels of continued lines can reach
        // into the tile buffer; those anchors are placed last (they would be
        // cut at the tile edge in standalone tile rendering), while MapLibre
        // strictly drops them because its cross-tile index lets the tile the
        // anchor belongs to place the label.
        var candidates = new List<(float Pos, bool InsideTile)>();

        for (var pos = offset; pos < pathLength; pos += spacing)
        {
            // The whole label must fit on the path around the anchor
            if (pos - halfLabelLength >= 0f && pos + halfLabelLength <= pathLength)
            {
                candidates.Add((pos, AnchorInsideTile(part, tile, pathLength, pos)));
            }
        }

        var anchors = candidates.Where(c => c.InsideTile).Select(c => c.Pos)
            .Concat(candidates.Where(c => !c.InsideTile).Select(c => c.Pos)).ToList();

        if (anchors.Count == 0 && !isLineContinued)
        {
            // No distributed position fits: fall back to one centered label
            // (MapLibre's placeAtMiddle retry)
            var center = pathLength / 2f;

            if (center - halfLabelLength >= 0f && center + halfLabelLength <= pathLength)
            {
                anchors.Add(center);
            }
        }

        return anchors;
    }

    /// <summary>
    /// Checks whether the position on the contour lies inside the tile
    /// rectangle. The path arclength fraction is preserved by the affine
    /// conversion, so the world position is interpolated along the world
    /// contour at the same fraction.
    /// </summary>
    private static bool AnchorInsideTile(LineString part, Tile tile, float pathLength, float pos)
    {
        var coordinates = part.Coordinates;

        var worldLength = 0.0;
        for (var i = 0; i < coordinates.Length - 1; i++)
        {
            worldLength += coordinates[i].Distance(coordinates[i + 1]);
        }

        var target = pathLength > 0 ? worldLength * (pos / pathLength) : 0.0;
        var distance = 0.0;

        for (var i = 0; i < coordinates.Length - 1; i++)
        {
            var segment = coordinates[i].Distance(coordinates[i + 1]);

            if (distance + segment >= target)
            {
                var t = segment > 0 ? (target - distance) / segment : 0.0;
                var x = coordinates[i].X + (coordinates[i + 1].X - coordinates[i].X) * t;
                var y = coordinates[i].Y + (coordinates[i + 1].Y - coordinates[i].Y) * t;

                return x >= tile.Left && x <= tile.Right && y >= tile.Bottom && y <= tile.Top;
            }

            distance += segment;
        }

        return false;
    }

    /// <summary>
    /// Returns the label length of the symbol in the placement screen space:
    /// the longer of the icon width and the measured text width (MapLibre takes
    /// the maximum of both shaped lengths).
    /// </summary>
    private static float GetLabelLength(LineSymbol symbol, EvaluationContext context)
    {
        var iconLength = symbol.IconSymbol != null
            ? IconLineSymbolRenderer.GetLabelLength(symbol.IconSymbol!)
            : 0f;

        var textLength = symbol.TextSymbol != null
            ? TextLineSymbolRenderer.GetLabelLength(symbol.TextSymbol!, context)
            : 0f;

        return Math.Max(iconLength, textLength);
    }

    /// <summary>
    /// A line is continued when its start lies outside the tile rectangle (on
    /// the border or in the tile buffer), like the boundary check of MapLibre's
    /// get_anchors.
    /// </summary>
    private static bool IsLineContinued(LineString part, Tile tile)
    {
        var first = part.Coordinates[0];
        var epsilon = (tile.Right - tile.Left) * 1e-6;

        return first.X < tile.Left - epsilon || first.X > tile.Right + epsilon ||
               first.Y < tile.Bottom - epsilon || first.Y > tile.Top + epsilon;
    }

    /// <summary>
    /// Converts the world coordinates of a geometry to screen coordinates and
    /// creates one path per contour of the geometry. Checking and drawing run
    /// per contour, so that labels of other contours do not influence the
    /// measured length of the current one.
    /// </summary>
    /// <param name="symbol">Symbol with geometry to convert.</param>
    /// <param name="worldToScreenConverter">Converter from world to screen coordinates.</param>
    /// <returns>Paths with screen coordinates and the world contour, one entry per contour.</returns>
    private static List<(SKPath Path, LineString Part)> CreateContourPaths(LineSymbol symbol,
        Func<double, double, (double, double)> worldToScreenConverter)
    {
        var paths = new List<(SKPath, LineString)>();

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

            paths.Add((path, part));
        }

        return paths;
    }
}
