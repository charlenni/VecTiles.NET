using Mapsui;
using Mapsui.Extensions;
using Mapsui.Tiling.Extensions;
using SkiaSharp;
using VecTiles.Controls.Mapsui;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Tests the affine image -> device transform used by DrawRetainedFrame:
/// the frame of a previous viewport must be mapped so its world content
/// stays at the correct world position and in the correct scale of the
/// new view (zoom boundary flash fix).
/// </summary>
public class RetainedFrameAffineTests
{
    private static readonly SKImageInfo CanvasInfo = new(800, 600);

    private static void AssertPointNear(SKMatrix matrix, float sx, float sy, float expectedX, float expectedY,
        string label, float tolerance = 0.5f)
    {
        var result = matrix.MapPoint(sx, sy);
        Assert.True(Math.Abs(result.X - expectedX) < tolerance && Math.Abs(result.Y - expectedY) < tolerance,
            $"{label}: ({sx},{sy}) mapped to ({result.X},{result.Y}) instead of ({expectedX},{expectedY})");
    }

    private static SKCanvas CreateCanvas()
    {
        return SKSurface.Create(CanvasInfo).Canvas;
    }

    /// <summary>
    /// Rebuilds the affine the layer's DrawRetainedFrame builds: maps the
    /// corners of the old image (in old device pixels) to their device
    /// positions in the current view.
    /// </summary>
    private static SKMatrix CreateAffineForViewportChange(Viewport oldViewport, SKCanvas canvas,
        Viewport current, float imageWidth, float imageHeight)
    {
        var corner0 = oldViewport.ScreenToWorld(0, 0);
        var corner1 = oldViewport.ScreenToWorld(imageWidth, 0);
        var corner2 = oldViewport.ScreenToWorld(0, imageHeight);
        return RenderedTileLayer.CreateAffine(
            new SKPoint(0, 0), DevicePosition(canvas, current, corner0.X, corner0.Y),
            new SKPoint(imageWidth, 0), DevicePosition(canvas, current, corner1.X, corner1.Y),
            new SKPoint(0, imageHeight), DevicePosition(canvas, current, corner2.X, corner2.Y));
    }

    private static SKPoint DevicePosition(SKCanvas canvas, Viewport viewport, double worldX, double worldY)
    {
        var logical = viewport.WorldToScreen(worldX, worldY);
        return canvas.TotalMatrix.MapPoint((float)logical.X, (float)logical.Y);
    }

    [Fact]
    public void SameViewport_MapsTheImageOneToOne()
    {
        var viewport = new Viewport(1000, 1000, 10, 0, 800, 600);
        var canvas = CreateCanvas();

        var matrix = CreateAffineForViewportChange(viewport, canvas, viewport, 800, 600);

        AssertPointNear(matrix, 0, 0, 0, 0, "top-left");
        AssertPointNear(matrix, 800, 0, 800, 0, "top-right");
        AssertPointNear(matrix, 0, 600, 0, 600, "bottom-left");
        AssertPointNear(matrix, 400, 300, 400, 300, "center");
    }

    [Fact]
    public void ZoomedIn_MapsTheWorldPointsToTheNewScale()
    {
        // The frame was drawn for resolution 10, the current view has
        // resolution 5 (one zoom level in, around the same center)
        var oldViewport = new Viewport(1000, 1000, 10, 0, 800, 600);
        var newViewport = new Viewport(1000, 1000, 5, 0, 800, 600);
        var canvas = CreateCanvas();

        var matrix = CreateAffineForViewportChange(oldViewport, canvas, newViewport, 800, 600);

        // The world center of the frame stays at the screen center
        var worldCenter = oldViewport.ScreenToWorld(400, 300);
        var worldCenterOnScreen = newViewport.WorldToScreen(worldCenter.X, worldCenter.Y);
        AssertPointNear(matrix, 400, 300, (float)worldCenterOnScreen.X, (float)worldCenterOnScreen.Y, "world center");

        // The image is drawn in double size (resolution halved): the corner
        // 300 px left of the center ends up 600 px left of the center
        AssertPointNear(matrix, 100, 300, (float)(400 - 2 * 300), 300, "x-scale");
        AssertPointNear(matrix, 400, 100, 400, (float)(300 - 2 * 200), "y-scale");

        // The world position of the top-left corner is where the new view
        // puts it (independent check against the WorldToScreen transform)
        var worldTopLeft = oldViewport.ScreenToWorld(0, 0);
        var device = DevicePosition(canvas, newViewport, worldTopLeft.X, worldTopLeft.Y);
        AssertPointNear(matrix, 0, 0, device.X, device.Y, "top-left");
    }

    [Fact]
    public void Panned_MapsTheWorldPointsToTheNewPosition()
    {
        // Same resolution, the viewport moved right by 200 px: the frame
        // content moves left by 200 px on the screen
        var oldViewport = new Viewport(1000, 1000, 10, 0, 800, 600);
        var newViewport = new Viewport(1000 + 200 * 10, 1000, 10, 0, 800, 600);
        var canvas = CreateCanvas();

        var matrix = CreateAffineForViewportChange(oldViewport, canvas, newViewport, 800, 600);

        AssertPointNear(matrix, 0, 0, -200, 0, "top-left");
        AssertPointNear(matrix, 400, 300, 200, 300, "center");
    }
}
