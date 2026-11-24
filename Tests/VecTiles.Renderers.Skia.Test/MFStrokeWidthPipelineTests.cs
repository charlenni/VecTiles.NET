using NetTopologySuite.Geometries;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Styles.Mapsforge;
using VecTiles.Styles.Mapsforge.Expressions;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Regression test for the stroke width of the Mapsforge line pipeline at
/// zoom 16: primary (stroke-width 1.5 in default.xml) must render at
/// 1.5 * 1.5^(16-12) = 7.59 px. Tests all three stages: the evaluated paint
/// value, the mesh expansion of the render factory (GetMaxStrokeWidth at
/// the next integer zoom, plus the antialias reserve) and the actually
/// rendered pixel width of <see cref="LineLayerRenderer.Draw"/> on a plain
/// (density 1) canvas.
/// </summary>
public class MFStrokeWidthPipelineTests
{
    private static Paint CreatePrimaryPaint()
    {
        var style = new MFLineLayerStyle("primary", MFAllExpression.Empty, 0, 256,
            new Color(255, 215, 0, 255), 1.5f, null, StrokeCap.Round, StrokeJoin.Round, null, 0f);
        var spriteFactory = new MFSpriteFactory("file://" + Path.Combine(
            Path.GetDirectoryName(typeof(MFStrokeWidthPipelineTests).Assembly.Location)!));
        return (Paint)new MFPaintFactory(spriteFactory).CreatePaint(style)!;
    }

    [Fact]
    public void PaintValueAtZoom16()
    {
        var paint = CreatePrimaryPaint();
        var ctx16 = new EvaluationContext(16f);
        paint.Update(ctx16);

        // 1.5 * 1.5^4
        Assert.Equal(7.59375f, paint.StrokeWidth, 5);
    }

    [Fact]
    public void MeshExpansionReservesMaxWidth()
    {
        var paint = CreatePrimaryPaint();
        var ctx16 = new EvaluationContext(16f);

        // Mesh is expanded for the widest stroke up to the next integer
        // zoom (1.5 * 1.5^5 = 11.39) plus the antialias reserve of 2
        Assert.Equal(13.390625f, paint.GetMaxStrokeWidth(ctx16) + 2f, 5);
    }

    [Fact]
    public void RenderedPixelWidthAtZoom16()
    {
        var paint = CreatePrimaryPaint();
        var ctx16 = new EvaluationContext(16f);

        // Horizontal line of 400 px in tile space, drawn at zoom 16, scale 1
        var geometry = new LineString(new[]
        {
            new Coordinate(50, 256),
            new Coordinate(450, 256),
        });
        var feature = new NetTopologySuite.Features.Feature(
            geometry, new NetTopologySuite.Features.AttributesTable());

        var maxStrokeWidth = paint.GetMaxStrokeWidth(ctx16) + 2;
        var builder = new Primitives.Vertices.VertexBuilder(1024);
        feature.ToVertices(builder, maxStrokeWidth, paint.StrokeCap, paint.StrokeCap, paint.StrokeJoin);

        using var renderer = new LineLayerRenderer(builder.Build(), false, paint, maxStrokeWidth);

        using var bitmap = new SKBitmap(512, 512);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        renderer.Draw(canvas, ctx16);

        // Count the yellow rows at the center column (the value 7.59 plus
        // the smoothstep antialias edge of about +/- 0.5 px per side)
        var yellowRows = 0;
        for (int y = 200; y < 312; y++)
        {
            var c = bitmap.GetPixel(256, y);
            if (c.Red > 128 && c.Green > 100 && c.Blue < 128)
            {
                yellowRows++;
            }
        }

        Assert.InRange(yellowRows, 6, 11);
    }
}
