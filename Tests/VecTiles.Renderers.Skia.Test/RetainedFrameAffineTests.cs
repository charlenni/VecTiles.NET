using Mapsui;
using Mapsui.Extensions;
using Mapsui.Tiling.Extensions;
using SkiaSharp;
using VecTiles.Controls.Mapsui;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Tests the affine transform that maps the retained frame of a previous
/// viewport into the current viewport: its world content must stay at the
/// correct world position and in the correct scale of the new view (zoom
/// boundary flash fix), including on HiDPI canvases.
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

    private static SKMatrix MatrixFor((double CX, double CY, double Res, double Rotation, double W, double H) old,
        double oldDensity, Viewport current, float density = 1)
    {
        var canvas = SKSurface.Create(CanvasInfo).Canvas;
        canvas.Scale(density, density);

        return RenderedTileLayer.CreateRetainedFrameMatrix(old,
            new SKRect(0, 0, (float)(old.W * oldDensity), (float)(old.H * oldDensity)),
            (float)(old.W * oldDensity), (float)(old.H * oldDensity), current, canvas.TotalMatrix)!.Value;
    }

    /// <summary>
    /// Verifies that for an unchanged viewport the retained frame is mapped one to one.
    /// </summary>
    [Fact]
    public void SameViewport_MapsTheImageOneToOne()
    {
        var old = (CX: 1000.0, CY: 1000.0, Res: 10.0, Rotation: 0.0, W: 800.0, H: 600.0);
        var current = new Viewport(1000, 1000, 10, 0, 800, 600);

        var matrix = MatrixFor(old, 1, current);

        AssertPointNear(matrix, 0, 0, 0, 0, "top-left");
        AssertPointNear(matrix, 800, 0, 800, 0, "top-right");
        AssertPointNear(matrix, 0, 600, 0, 600, "bottom-left");
        AssertPointNear(matrix, 400, 300, 400, 300, "center");
    }

    /// <summary>
    /// Verifies that with a density 2 canvas the retained frame image is mapped
    /// one to one in device pixels, without a second density scaling.
    /// </summary>
    [Fact]
    public void SameViewport_HighDpi_MapsTheImageOneToOneInDevicePixels()
    {
        // Density 2: the image has 1600x1200 device pixels for a 800x600
        // logical viewport; the map starts at logical scale as if density
        // was 1, so the image must be drawn in double logical size, i.e.
        // 1:1 in device pixels — this guards against the frozen-pixel
        // mapping, which scaled the image a second time by the density
        var old = (CX: 1000.0, CY: 1000.0, Res: 10.0, Rotation: 0.0, W: 800.0, H: 600.0);
        var current = new Viewport(1000, 1000, 10, 0, 800, 600);

        var matrix = MatrixFor(old, 2, current, density: 2);

        // device = change of scale: the logical point p is at image pixel 2p;
        // the image pixel 2p must land at device 2p (the device transform of
        // the logical point p under the density 2 canvas matrix)
        AssertPointNear(matrix, 0, 0, 0, 0, "top-left");
        AssertPointNear(matrix, 1600, 0, 1600, 0, "top-right");
        AssertPointNear(matrix, 0, 1200, 0, 1200, "bottom-left");
        AssertPointNear(matrix, 800, 600, 800, 600, "center");
    }

    /// <summary>
    /// Verifies that after zooming in the retained frame maps the world points
    /// to the scale of the new resolution: twice the draw size, same world center.
    /// </summary>
    [Fact]
    public void ZoomedIn_MapsTheWorldPointsToTheNewScale()
    {
        // The frame was drawn for resolution 10, the current view has
        // resolution 5 (one zoom level in, around the same center)
        var old = (CX: 1000.0, CY: 1000.0, Res: 10.0, Rotation: 0.0, W: 800.0, H: 600.0);
        var current = new Viewport(1000, 1000, 5, 0, 800, 600);

        var matrix = MatrixFor(old, 1, current);

        // The world center of the frame stays at the screen center
        AssertPointNear(matrix, 400, 300, 400, 300, "world center");

        // The image is drawn in double size (resolution halved): the image
        // point 300 px left of the center ends up 600 px left of the center
        AssertPointNear(matrix, 100, 300, -200, 300, "x-scale");
        AssertPointNear(matrix, 400, 100, 400, -100, "y-scale");
    }

    /// <summary>
    /// Verifies that after panning the retained frame content is mapped to the
    /// new screen position, shifted by the pan distance.
    /// </summary>
    [Fact]
    public void Panned_MapsTheWorldPointsToTheNewPosition()
    {
        // Same resolution, the viewport moved right by 200 px: the frame
        // content moves left by 200 px on the screen
        var old = (CX: 1000.0, CY: 1000.0, Res: 10.0, Rotation: 0.0, W: 800.0, H: 600.0);
        var current = new Viewport(1000 + 200 * 10, 1000, 10, 0, 800, 600);

        var matrix = MatrixFor(old, 1, current);

        AssertPointNear(matrix, 0, 0, -200, 0, "top-left");
        AssertPointNear(matrix, 400, 300, 200, 300, "center");
    }
}
