using System.Diagnostics;
using NetTopologySuite.Features;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Common.Interfaces;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Renderers.Skia.Primitives.Vertices;

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

        if (!_paints.TryGetValue(style.Name, out var paint))
        {
            return null;
        }

        features = SortBySortKey(style, context, features, FeatureSortKey.Fill);

        // Data-driven paint? Then every group of features with equal values
        // gets its own paint (the snapshot of the paint for the first feature
        // of the group) and renderer.
        if (paint.IsDataDriven)
        {
            var renderers = new List<ILayerRenderer>();

            foreach (var (groupPaint, group) in GroupFeaturesByPaint(features!, f =>
                         paint.SnapshotFor(context.WithFeature(f))))
            {
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
                        feature.ToVertices(groupOutlineBuilder, groupPaint.GetMaxStrokeWidth(context), groupPaint.StrokeJoin,
                            groupPaint.StrokeMiter);
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
                feature.ToVertices(outlineBuilder, paint.GetMaxStrokeWidth(context),
                    paint.StrokeJoin, paint.StrokeMiter);
            }

            return new FillLayerRenderer(fillBuilder.Build(), outlineBuilder.Build(), true, paint);
        }

        return new FillLayerRenderer(fillBuilder.Build(), true, paint);
    }

    /// <inheritdoc />
    public ILayerRenderer CreateVectorLineLayer(ILayerStyle style,
        EvaluationContext context,
        IEnumerable<IFeature> features)
    {
        if (!_paints.TryGetValue(style.Name, out var paint))
        {
            return null;
        }

        features = SortBySortKey(style, context, features, FeatureSortKey.Line);

        // Data-driven paint? Then every group of features with equal values
        // gets its own paint (the snapshot of the paint for the first feature
        // of the group) and renderer.
        if (paint.IsDataDriven)
        {
            var renderers = new List<ILayerRenderer>();

            foreach (var (groupPaint, group) in GroupFeaturesByPaint(features!, f =>
                         paint.SnapshotFor(context.WithFeature(f))))
            {
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
                        groupPaint.StrokeJoin,
                        groupPaint.StrokeMiter);
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
        var maxStrokeWidth = paint.GetMaxStrokeWidth(context) + 2;  // Small adjustments for AA

        // Create vertices for features that belong to a line style (only the line, no border)
        foreach (var feature in features!)
        {
            feature.ToVertices(builder, maxStrokeWidth ,
                paint.StrokeCap,
                paint.StrokeCap,
                paint.StrokeJoin,
                paint.StrokeMiter);
        }

        return new LineLayerRenderer(builder.Build(), false, paint, maxStrokeWidth);
    }

    /// <inheritdoc />
    public ILayerRenderer CreateVectorCircleLayer(ILayerStyle style,
        EvaluationContext context,
        IEnumerable<IFeature> features)
    {
        if (!_paints.TryGetValue(style.Name, out var paint))
        {
            return null;
        }

        var sortedFeatures = SortBySortKey(style, context, features, FeatureSortKey.Circle);

        // Data-driven paint? Then every group of features with equal values
        // gets its own paint (the snapshot of the paint for the first feature
        // of the group) and renderer.
        if (paint.IsDataDriven)
        {
            var renderers = new List<ILayerRenderer>();

            foreach (var (groupPaint, group) in GroupFeaturesByPaint(sortedFeatures, f =>
                         paint.SnapshotFor(context.WithFeature(f))))
            {
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

        return new CircleLayerRenderer([.. points], paint);
    }

    /// <summary>
    /// Sorts features in ascending order by the evaluated sort-key layout
    /// property of the style (see <see cref="IFeatureSorting"/>); features
    /// with a higher key are drawn later and appear on top. Styles without
    /// sort keys keep the source order.
    /// </summary>
    private static IEnumerable<IFeature> SortBySortKey(ILayerStyle style, EvaluationContext context,
        IEnumerable<IFeature> features, FeatureSortKey sortKey) =>
        style is IFeatureSorting sortable
            ? sortable.SortFeatures(context, features, sortKey)
            : [.. features];

    /// <summary>
    /// Groups features by their data-driven paint snapshot: features whose
    /// paint properties evaluate equally belong to the same group, groups
    /// keep the (sorted) order of their first feature, and each group carries
    /// the snapshot paint of its first feature.
    /// </summary>
    private static List<(IPaint Paint, List<IFeature> Features)> GroupFeaturesByPaint(
        IEnumerable<IFeature> features, Func<IFeature, IPaint> snapshotFor)
    {
        var groups = new Dictionary<string, (IPaint Paint, List<IFeature> Features)>();
        var result = new List<(IPaint, List<IFeature>)>();

        foreach (var feature in features)
        {
            var paint = snapshotFor(feature);
            var key = PaintSignature(paint);

            if (!groups.TryGetValue(key, out var group))
            {
                group = (paint, new List<IFeature>());
                groups.Add(key, group);
                result.Add(group);
            }

            group.Features.Add(feature);
        }

        return result;
    }

    /// <summary>
    /// Signature of the evaluated paint values: features whose snapshot
    /// paints have equal signatures draw identically. Constant properties
    /// evaluate equally for every feature, so including them in the
    /// signature does not create additional groups.
    /// </summary>
    private static string PaintSignature(IPaint paint)
    {
        return $"{paint.Style};{paint.Color};{paint.Opacity};{paint.OutlineColor};{paint.IsAntialias};" +
               $"{paint.StrokeWidth};{paint.StrokeCap};{paint.StrokeJoin};{paint.StrokeMiter};" +
               $"{paint.Pattern?.GetHashCode()};{paint.DashArray};{paint.Radius};{paint.Blur}";
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
