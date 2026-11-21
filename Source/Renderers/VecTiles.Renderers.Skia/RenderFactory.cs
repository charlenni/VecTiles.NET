using System.Diagnostics;
using NetTopologySuite.Features;
using SkiaSharp;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Renderers.Skia.Primitives.Vertices;
using VecTiles.Styles.OpenMapTiles;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Renderers.Skia;

/// <summary>
/// Skia implementation of <see cref="IRenderFactory" />. Converts the
/// features of a vector tile into drawable layer renderers for the given
/// style, creating the tessellated vertex buffers at render time.
/// </summary>
public class RenderFactory : IRenderFactory
{
    static readonly SKRect _tileRect = new SKRect(0, 0, 512, 512);

    Dictionary<string, IPaint> _paints;
    IPaintFactory _paintFactory;
    ISymbolFactory _symbolFactory;
    private ISymbolRenderer _pointSymbolRenderer = new PointSymbolRenderer();
    private ISymbolRenderer _iconPointSymbolRenderer = new IconPointSymbolRenderer();

    /// <summary>
    /// Create a render factory that prepares a paint for each given style.
    /// </summary>
    /// <param name="styles">Styles to create paints for.</param>
    /// <param name="paintFactory">Factory to create the paints of geometry layers.</param>
    /// <param name="symbolFactory">Factory to create the symbols of symbol layers.</param>
    public RenderFactory(IEnumerable<ILayerStyle> styles, IPaintFactory paintFactory, ISymbolFactory symbolFactory)
    {
        _symbolFactory = symbolFactory;
        _paintFactory = paintFactory;
        _paints = new Dictionary<string, IPaint>(styles.Count());

        // Create for each style a IPaint, which then creates later a SKPaint for a given evaluation context
        foreach (var style in styles)
        {
            var paint = paintFactory.CreatePaint(style);

            if (paint != null)
            {
                _paints.Add(style.Name, paint);
            }
        }
    }

    /// <inheritdoc />
    public ILayerRenderer CreateBackgroundLayer(ILayerStyle style)
    {
        return new BackgroundLayerRenderer(_tileRect, _paints[style.Name]);
    }

    /// <inheritdoc />
    public ILayerRenderer CreateRasterLayer(ILayerStyle style, byte[] data)
    {
        using var bitmap = SKBitmap.Decode(data);

        if (bitmap == null)
        {
            throw new Exception("Not possible to decode image");
        }

        return new RasterLayerRenderer(_tileRect, _paints[style.Name], bitmap);
    }

    /// <inheritdoc />
    public ILayerRenderer CreateVectorFillLayer(ILayerStyle style,
        EvaluationContext context,
        IEnumerable<IFeature> features)
    {
        // A filled area consits of a polygon, that is filled and a outline around it
        // Both need different vertices. So we need two types of vertices.

        if (!_paints.ContainsKey(style.Name))
        {
            return null;
        }

        features = SortBySortKey(style, context, features, s => s.Layout.FillSortKey);

        // Data-driven paint? Then every group of features with equal values
        // gets its own paint and renderer.
        if (style is OMTLayerStyle fillStyle &&
            (fillStyle.Paint.FillColor.Expression != null ||
             fillStyle.Paint.FillOpacity.Expression != null ||
             fillStyle.Paint.FillOutlineColor.Expression != null))
        {
            var renderers = new List<ILayerRenderer>();

            foreach (var group in GroupFeatures(features!, f =>
            {
                var fc = context.WithFeature(f);
                var paint = fillStyle.Paint;
                return $"{(paint.FillColor.Expression != null ? paint.FillColor.Evaluate(fc) : (object?)string.Empty)};" +
                       $"{(paint.FillOpacity.Expression != null ? paint.FillOpacity.Evaluate(fc) : (object?)string.Empty)};" +
                       $"{(paint.FillOutlineColor.Expression != null ? paint.FillOutlineColor.Evaluate(fc) : (object?)string.Empty)}";
            }))
            {
                var groupPaint = CreateGroupPaint(fillStyle, context.WithFeature(group[0]), (brush, featureContext) =>
                {
                    if (fillStyle.Paint.FillColor.Expression != null)
                        brush.SetFixColor(fillStyle.Paint.FillColor.Evaluate(featureContext));
                    if (fillStyle.Paint.FillOpacity.Expression != null)
                        brush.SetFixOpacity(fillStyle.Paint.FillOpacity.Evaluate(featureContext));
                    if (fillStyle.Paint.FillOutlineColor.Expression != null)
                        brush.SetFixOutlineColor(fillStyle.Paint.FillOutlineColor.Evaluate(featureContext));
                });

                var groupFillBuilder = new VertexBuilder();
                foreach (var feature in group)
                {
                    feature.ToVertices(groupFillBuilder, groupPaint.StrokeJoin);
                }

                if (groupPaint.IsAntialias && groupPaint.OutlineColor != groupPaint.Color)
                {
                    var groupOutlineBuilder = new VertexBuilder();
                    foreach (var feature in group)
                    {
                        feature.ToVertices(groupOutlineBuilder, 1f, groupPaint.StrokeJoin);
                    }

                    renderers.Add(new FillLayerRenderer(groupFillBuilder.Build(), groupOutlineBuilder.Build(), true, groupPaint));
                }
                else
                {
                    renderers.Add(new FillLayerRenderer(groupFillBuilder.Build(), true, groupPaint));
                }
            }

            return new CompositeLayerRenderer(renderers);
        }

        var fillBuilder = new VertexBuilder();
        var outlineBuilder = new VertexBuilder();
        var paint = _paints[style.Name];

        // Create vertices for features that belong to a fill style (only fill area)
        foreach (var feature in features!)
        {
            feature.ToVertices(fillBuilder, paint.StrokeJoin);
        }

        if (paint.IsAntialias && paint.OutlineColor != paint.Color)
        {
            // Create vertices for features that belong to a outline style (only stroke around area)
            foreach (var feature in features!)
            {
                feature.ToVertices(outlineBuilder, 1f,
                    paint.StrokeJoin);
            }

            return new FillLayerRenderer(fillBuilder.Build(), outlineBuilder.Build(), true, _paints[style.Name]);
        }

        return new FillLayerRenderer(fillBuilder.Build(), true, _paints[style.Name]);
    }

    /// <inheritdoc />
    public ILayerRenderer CreateVectorLineLayer(ILayerStyle style, 
        EvaluationContext context, 
        IEnumerable<IFeature> features)
    {
        if (!_paints.ContainsKey(style.Name))
        {
            return null;
        }

        features = SortBySortKey(style, context, features, s => s.Layout.LineSortKey);

        // Data-driven paint? Then every group of features with equal values
        // gets its own paint and renderer.
        if (style is OMTLayerStyle lineStyle &&
            (lineStyle.Paint.LineColor.Expression != null ||
             lineStyle.Paint.LineOpacity.Expression != null ||
             lineStyle.Paint.LineWidth.Expression != null))
        {
            var renderers = new List<ILayerRenderer>();

            foreach (var group in GroupFeatures(features!, f =>
            {
                var fc = context.WithFeature(f);
                var paint = lineStyle.Paint;
                return $"{(paint.LineColor.Expression != null ? paint.LineColor.Evaluate(fc) : (object?)string.Empty)};" +
                       $"{(paint.LineOpacity.Expression != null ? paint.LineOpacity.Evaluate(fc) : (object?)string.Empty)};" +
                       $"{(paint.LineWidth.Expression != null ? paint.LineWidth.Evaluate(fc) : (object?)string.Empty)}";
            }))
            {
                var groupPaint = CreateGroupPaint(lineStyle, context.WithFeature(group[0]), (brush, featureContext) =>
                {
                    if (lineStyle.Paint.LineColor.Expression != null)
                        brush.SetFixColor(lineStyle.Paint.LineColor.Evaluate(featureContext));
                    if (lineStyle.Paint.LineOpacity.Expression != null)
                        brush.SetFixOpacity(lineStyle.Paint.LineOpacity.Evaluate(featureContext));
                    if (lineStyle.Paint.LineWidth.Expression != null)
                        brush.SetFixStrokeWidth(lineStyle.Paint.LineWidth.Evaluate(featureContext));
                });

                // Calc number of possible vertices
                var groupNumOfPoints = 0;

                foreach (var feature in group)
                {
                    groupNumOfPoints += feature.Geometry.NumPoints;
                }

                var groupBuilder = new VertexBuilder(groupNumOfPoints * 3);
                var groupMaxStrokeWidth = groupPaint.GetMaxStrokeWidth(context) + 2;  // Small adjustments for AA

                foreach (var feature in group)
                {
                    feature.ToVertices(groupBuilder, groupMaxStrokeWidth,
                        groupPaint.StrokeCap,
                        groupPaint.StrokeCap,
                        groupPaint.StrokeJoin);
                }

                renderers.Add(new LineLayerRenderer(groupBuilder.Build(), false, groupPaint, groupMaxStrokeWidth));
            }

            return new CompositeLayerRenderer(renderers);
        }

        // Calc number of possible vertices
        var numOfPoints = 0;

        foreach (var feature in features!)
        {
            numOfPoints += feature.Geometry.NumPoints;
        }

        var builder = new VertexBuilder(numOfPoints * 3);
        var paint = _paints[style.Name];
        var maxStrokeWidth = paint.GetMaxStrokeWidth(context) + 2;  // Small adjustments for AA

        // Create vertices for features that belong to a line style (only the line, no border)
        foreach (var feature in features!)
        {
            feature.ToVertices(builder, maxStrokeWidth ,
                paint.StrokeCap,
                paint.StrokeCap, 
                paint.StrokeJoin);
        }

        return new LineLayerRenderer(builder.Build(), false, _paints[style.Name], maxStrokeWidth);
    }

    /// <inheritdoc />
    public ILayerRenderer CreateVectorCircleLayer(ILayerStyle style,
        EvaluationContext context,
        IEnumerable<IFeature> features)
    {
        if (!_paints.ContainsKey(style.Name))
        {
            return null;
        }

        var sortedFeatures = SortBySortKey(style, context, features, s => s.Layout.CircleSortKey);

        // Data-driven paint? Then every group of features with equal values
        // gets its own paint and renderer.
        if (style is OMTLayerStyle circleStyle &&
            (circleStyle.Paint.CircleColor.Expression != null ||
             circleStyle.Paint.CircleOpacity.Expression != null ||
             circleStyle.Paint.CircleRadius.Expression != null ||
             circleStyle.Paint.CircleBlur.Expression != null ||
             circleStyle.Paint.CircleStrokeColor.Expression != null ||
             circleStyle.Paint.CircleStrokeOpacity.Expression != null ||
             circleStyle.Paint.CircleStrokeWidth.Expression != null))
        {
            var renderers = new List<ILayerRenderer>();

            foreach (var group in GroupFeatures(sortedFeatures, f =>
            {
                var fc = context.WithFeature(f);
                var paint = circleStyle.Paint;
                return $"{(paint.CircleColor.Expression != null ? paint.CircleColor.Evaluate(fc) : (object?)string.Empty)};" +
                       $"{(paint.CircleOpacity.Expression != null ? paint.CircleOpacity.Evaluate(fc) : (object?)string.Empty)};" +
                       $"{(paint.CircleRadius.Expression != null ? paint.CircleRadius.Evaluate(fc) : (object?)string.Empty)};" +
                       $"{(paint.CircleBlur.Expression != null ? paint.CircleBlur.Evaluate(fc) : (object?)string.Empty)};" +
                       $"{(paint.CircleStrokeColor.Expression != null ? paint.CircleStrokeColor.Evaluate(fc) : (object?)string.Empty)};" +
                       $"{(paint.CircleStrokeOpacity.Expression != null ? paint.CircleStrokeOpacity.Evaluate(fc) : (object?)string.Empty)};" +
                       $"{(paint.CircleStrokeWidth.Expression != null ? paint.CircleStrokeWidth.Evaluate(fc) : (object?)string.Empty)}";
            }))
            {
                var groupPaint = CreateGroupPaint(circleStyle, context.WithFeature(group[0]), (brush, featureContext) =>
                {
                    var p = circleStyle.Paint;
                    if (p.CircleColor.Expression != null)
                        brush.SetFixColor(p.CircleColor.Evaluate(featureContext));
                    if (p.CircleOpacity.Expression != null)
                        brush.SetFixOpacity(p.CircleOpacity.Evaluate(featureContext));
                    if (p.CircleRadius.Expression != null)
                        brush.SetFixRadius(p.CircleRadius.Evaluate(featureContext));
                    if (p.CircleBlur.Expression != null)
                        brush.SetFixBlur(p.CircleBlur.Evaluate(featureContext));
                    if (p.CircleStrokeColor.Expression != null)
                        brush.SetFixOutlineColor(p.CircleStrokeColor.Evaluate(featureContext));
                    if (p.CircleStrokeOpacity.Expression != null)
                        brush.SetFixOutlineOpacity(p.CircleStrokeOpacity.Evaluate(featureContext));
                    if (p.CircleStrokeWidth.Expression != null)
                        brush.SetFixStrokeWidth(p.CircleStrokeWidth.Evaluate(featureContext));
                });

                // Per Mapbox spec a circle is drawn at every vertex of the geometry
                var groupPoints = new List<SKPoint>();

                foreach (var feature in group)
                {
                    foreach (var coordinate in feature.Geometry.Coordinates)
                    {
                        groupPoints.Add(new SKPoint((float)coordinate.X, (float)coordinate.Y));
                    }
                }

                renderers.Add(new CircleLayerRenderer([.. groupPoints], groupPaint));
            }

            return new CompositeLayerRenderer(renderers);
        }

        // Per Mapbox spec a circle is drawn at every vertex of the geometry
        var points = new List<SKPoint>();

        foreach (var feature in sortedFeatures)
        {
            foreach (var coordinate in feature.Geometry.Coordinates)
            {
                points.Add(new SKPoint((float)coordinate.X, (float)coordinate.Y));
            }
        }

        return new CircleLayerRenderer([.. points], _paints[style.Name]);
    }

    /// <summary>
    /// Sorts features in ascending order by the evaluated sort-key layout
    /// property. Features with a higher key are drawn later and appear on top
    /// (Mapbox spec). Equal keys keep their source order (stable sort). Only a
    /// data-driven key (an expression) can differ per feature, so sorting is
    /// done only in that case; constant keys (no stops / no expression) give
    /// every feature the same key and keep the source order.
    /// </summary>
    private static List<IFeature> SortBySortKey(ILayerStyle style, EvaluationContext context,
        IEnumerable<IFeature> features, Func<OMTLayerStyle, StoppedFloat> sortKey)
    {
        if (style is not OMTLayerStyle omtStyle || sortKey(omtStyle).Expression == null)
        {
            return [.. features];
        }

        return [.. features
            .Select(f => (Feature: f, Key: sortKey(omtStyle).Evaluate(context.WithFeature(f))))
            .OrderBy(t => t.Key)
            .Select(t => t.Feature)];
    }

    /// <summary>
    /// Groups features by a per-feature signature. Features with an equal
    /// signature belong to the same group, groups keep the (sorted) order of
    /// their first feature. Used for data-driven paint properties, so every
    /// group can be drawn with its own paint.
    /// </summary>
    private static List<List<IFeature>> GroupFeatures(IEnumerable<IFeature> features,
        Func<IFeature, string> signature)
    {
        var groups = new Dictionary<string, List<IFeature>>();
        var result = new List<List<IFeature>>();

        foreach (var feature in features)
        {
            var key = signature(feature);

            if (!groups.TryGetValue(key, out var group))
            {
                group = new List<IFeature>();
                groups[key] = group;
                result.Add(group);
            }

            group.Add(feature);
        }

        return result;
    }

    /// <summary>
    /// Creates a paint for a group of features with equal data-driven paint
    /// values. The paint is created from the style, afterwards the evaluated
    /// data-driven values are applied as fix values.
    /// </summary>
    private IPaint CreateGroupPaint(OMTLayerStyle style, EvaluationContext featureContext,
        Action<Paint, EvaluationContext> apply)
    {
        var paint = _paintFactory.CreatePaint(style) as Paint
            ?? throw new InvalidOperationException($"Couldn't create a group paint for layer '{style.Name}'");

        apply(paint, featureContext);

        return paint;
    }

    /// <inheritdoc />
    public ISymbol? CreateSymbol(Tile tile,
        ILayerStyle style, 
        EvaluationContext context, 
        IFeature feature)
    {
        return _symbolFactory.CreateSymbol(tile, style, context, feature);
    }
}
