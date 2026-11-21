using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Skia.Test;

/// <summary>
/// Visual tests for <see cref="IconPointSymbol" />: draws symbols with every
/// supported style property and compares the rendered bitmaps with the saved originals.
/// </summary>
public class IconPointSymbolTests
{
    private readonly string Folder = Path.Combine(Utilities.DataFiles);
    private readonly string OriginalsFolder = Path.Combine(Utilities.TestFiles, "Originals");
    private readonly string ResultsFolder = Path.Combine(Utilities.TestFiles, "Results");
    private SKBitmap _bitmap;
    private SKCanvas _canvas;
    private SKPaint _paint = new SKPaint();
    private SKFont _font = new SKFont(SKTypeface.FromFamilyName("Roboto", SKFontStyle.Bold));
    private Sprite _sprite;
    private EvaluationContext _context = new EvaluationContext(0, 1, 0);

    /// <summary>
    /// Prepares a 201x201 canvas centered at (100, 100) and loads the osm-liberty sprite atlas.
    /// </summary>
    public IconPointSymbolTests()
    {
        _bitmap = Utilities.CreateBitmap(201, 201);
        _canvas = new SKCanvas(_bitmap);

        // Prepare canvas
        _canvas.Clear(SKColors.LightGray);
        _canvas.Translate(100, 100);

        // Prepare sprite
        var filename = Path.Combine(Folder, "Sprites", "osm-liberty.png");
        var file = File.OpenRead(filename);
        var buffer = new byte[file.Length];

        file.ReadExactly(buffer, 0, (int)file.Length);

        var atlas = new Bitmap(buffer);
        
        _sprite = new Sprite(atlas, 168, 223, 19, 19);
    }

    /// <summary>
    /// Draws the icon with the given unanchored offset and compares the result with the original image.
    /// </summary>
    /// <param name="x">Offset in x direction in pixels.</param>
    /// <param name="y">Offset in y direction in pixels.</param>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(20.0, 0.0)]
    [InlineData(0.0, 20.0)]
    public void OffsetTest(double x, double y)
    {
        var symbol = new IconPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), _sprite)
        {
            Anchor = new Point(0, 0),
            Scale = 1,
            Offset = new Point(x, y),
        };

        Assert.True(DrawAndCompare(symbol, _context, $"Icon-Offset-{x}x-{y}y.png"));
    }

    /// <summary>
    /// Draws the icon with the given offset and a scale factor of 2 and compares the result with the original image.
    /// </summary>
    /// <param name="x">Offset in x direction in pixels.</param>
    /// <param name="y">Offset in y direction in pixels.</param>
    /// <param name="scale">The scale factor applied to the symbol.</param>
    [Theory]
    [InlineData(0.0, 0.0, 2.0)]
    [InlineData(20.0, 0.0, 2.0)]
    [InlineData(0.0, 20.0, 2.0)]
    public void OffsetWithScaleTest(double x, double y, double scale)
    {
        var symbol = new IconPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), _sprite)
        {
            Anchor = new Point(0, 0),
            Scale = (float)scale,
            Offset = new Point(x, y),
        };

        Assert.True(DrawAndCompare(symbol, _context, $"Icon-OffsetWithScale-{x}x-{y}y-{scale}scale.png"));
    }

    /// <summary>
    /// Verifies that a sprite from a 2x pixel ratio atlas is drawn at its CSS pixel size
    /// (half the atlas pixel size) and occupies an envelope of that size.
    /// </summary>
    [Fact]
    public void DrawsPixelRatioTwoSpriteAtCssSize()
    {
        // Fuel_11 sprite from the 2x atlas: 30x30 atlas pixels at pixel ratio 2 => 15x15 screen pixels
        var filename = Path.Combine(Folder, "Sprites", "osm-liberty@2x.png");
        var file = File.OpenRead(filename);
        var buffer = new byte[file.Length];

        file.ReadExactly(buffer, 0, (int)file.Length);

        var sprite = new Sprite(new Bitmap(buffer), 710, 94, 30, 30, 2f);

        var symbol = new IconPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), sprite)
        {
            Scale = 1,
        };

        var context = new EvaluationContext(0, 1, 0);

        using var bitmap = Utilities.CreateBitmap(31, 31);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var tree = new Quadtree<ISymbol>();
        Assert.Equal(SymbolPlacementStatus.Success,
            IconPointSymbolRenderer.CheckForSpace(canvas, context, symbol, tree, 15, 15, false));

        // The collision envelope must have the CSS pixel size of the icon
        Assert.Equal(15, symbol.Envelope!.MaxX - symbol.Envelope.MinX);
        Assert.Equal(15, symbol.Envelope!.MaxY - symbol.Envelope.MinY);

        IconPointSymbolRenderer.DrawIcon(canvas, context, symbol, 15, 15);

        // The drawn icon must be 15x15 CSS pixels; depending on the sub pixel position and
        // antialiasing, 14 to 16 pixels light up
        var (minX, minY, maxX, maxY) = MeasureContent(bitmap);

        Assert.InRange(maxX - minX + 1, 14, 16);
        Assert.InRange(maxY - minY + 1, 14, 16);
    }

    private static (int MinX, int MinY, int MaxX, int MaxY) MeasureContent(SKBitmap bitmap)
    {
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha <= 190)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return (minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Draws the icon anchored at the given relative anchor point and compares the result with the original image.
    /// </summary>
    /// <param name="x">Relative anchor position in x direction (0 to 1).</param>
    /// <param name="y">Relative anchor position in y direction (0 to 1).</param>
    [Theory]
    [InlineData(0.5, 0.5)]
    [InlineData(0, 0.5)]
    [InlineData(1, 0.5)]
    [InlineData(0.5, 0)]
    [InlineData(0.5, 1)]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    public void AnchorTest(double x, double y)    {
        var symbol = new IconPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), _sprite)
        {
            Scale = 1,
            Anchor = new Point(x, y),
        };

        Assert.True(DrawAndCompare(symbol, _context, $"Icon-Anchor-{x*100}x-{y*100}y.png"));
    }

    /// <summary>
    /// Draws the icon with the given opacity and compares the result with the original image.
    /// </summary>
    /// <param name="opacity">The opacity applied to the symbol (0 to 1).</param>
    [Theory]
    [InlineData(1.0)]
    [InlineData(0.5)]
    [InlineData(0.25)]
    public void OpacityTest(double opacity)
    {
        var symbol = new IconPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), _sprite)
        {
            Anchor = new Point(0, 0),
            Scale = 1,
            Opacity = ctx => (float)opacity
        };

        Assert.True(DrawAndCompare(symbol, _context, $"Icon-Opacity-{opacity * 100}.png"));
    }

    /// <summary>
    /// Draws the icon with the given translate distance and compares the result with the original image.
    /// </summary>
    /// <param name="tx">Translate distance in x direction in pixels.</param>
    /// <param name="ty">Translate distance in y direction in pixels.</param>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(10.0, 0.0)]
    [InlineData(0.0, 10.0)]
    public void TranslateTest(double tx, double ty)
    {
        var symbol = new IconPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), _sprite)
        {
            Anchor = new Point(0, 0),
            Scale = 1,
            Translate = ctx => new Point(tx, ty)
        };

        Assert.True(DrawAndCompare(symbol, _context, $"Icon-Translate-{tx}x-{ty}y.png"));
    }

    /// <summary>
    /// Draws the icon with a translate offset under a rotated map for both translate anchors
    /// and both rotation alignments, comparing the result with the original image.
    /// </summary>
    /// <param name="translateAnchor">The <see cref="MapAlignment" /> value of the translate anchor.</param>
    /// <param name="alignment">The <see cref="MapAlignment" /> value of the rotation alignment.</param>
    /// <param name="mapRotation">The rotation of the map in degrees.</param>
    [Theory]
    [InlineData(0, 0, 75)] // Map
    [InlineData(0, 1, 75)] // Map
    [InlineData(1, 0, 75)] // Viewport
    [InlineData(1, 1, 75)] // Viewport
    public void TranslateAnchorTest(int translateAnchor, int alignment, float mapRotation)
    {
        var anchor = (MapAlignment)translateAnchor;
        var rotationAlignment = (MapAlignment)alignment;
        var rotationContext = new EvaluationContext(0, 1, mapRotation);
        var symbol = new IconPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), _sprite)
        {
            Anchor = new Point(0, 0),
            Scale = 1,
            RotationAlignment = rotationAlignment,
            Translate = ctx => new Point(30, 10),
            TranslateAnchor = ctx => anchor
        };

        Assert.True(DrawAndCompare(symbol, rotationContext, $"Icon-TranslateAnchor-{anchor}-Alignment-{rotationAlignment}.png"));
    }

    /// <summary>
    /// Draws the icon rotated by the given angle and compares the result with the original image.
    /// </summary>
    /// <param name="rotation">The rotation in degrees.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(135)]
    [InlineData(180)]
    [InlineData(225)]
    [InlineData(270)]
    [InlineData(315)]
    [InlineData(360)]
    public void RotationTest(double rotation)
    {
        var symbol = new IconPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), _sprite)
        {
            Anchor = new Point(0, 0),
            Scale = 1,
            Rotation = (float)rotation
        };

        Assert.True(DrawAndCompare(symbol, _context, $"Icon-Rotation-{rotation}.png"));
    }

    /// <summary>
    /// Draws the rotated icon under a rotated map for each rotation alignment
    /// and compares the result with the original image.
    /// </summary>
    /// <param name="alignment">The <see cref="MapAlignment" /> value of the rotation alignment.</param>
    /// <param name="rotation">The rotation of the symbol in degrees.</param>
    /// <param name="mapRotation">The rotation of the map in degrees.</param>
    [Theory]
    [InlineData(0, 15, 30)] // Map
    [InlineData(1, 15, 30)] // Viewport
    [InlineData(0, 30, 30)] // Map
    [InlineData(1, 30, 30)] // Viewport
    [InlineData(0, 45, 30)] // Map
    [InlineData(1, 45, 30)] // Viewport
    [InlineData(2, 45, 30)] // Auto
    public void RotationAlignmentTest(int alignment, float rotation, float mapRotation)
    {
        var align = (MapAlignment)alignment;
        var rotationContext = new EvaluationContext(0, 1, mapRotation);
        var symbol = new IconPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), _sprite)
        {
            Anchor = new Point(0, 0),
            Scale = 1,
            Rotation = rotation,
            RotationAlignment = align
        };

        Assert.True(DrawAndCompare(symbol, rotationContext, $"Icon-RotationAlignment-{align}-{rotation}-{mapRotation}.png"));
    }

    /// <summary>
    /// Draws the icon with the given padding around the symbol and compares the result with the original image.
    /// </summary>
    /// <param name="padding">The padding around the symbol in pixels.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(8)]
    public void PaddingTest(int padding)
    {
        var symbol = new IconPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), _sprite)
        {
            Anchor = new Point(0, 0),
            Scale = 1,
            Padding = padding
        };

        Assert.True(DrawAndCompare(symbol, _context, $"Icon-Padding-{padding}.png"));
    }

    /// <summary>
    /// Draws the icon with and without keep-upright at four rotations and compares
    /// the result with the original image.
    /// </summary>
    /// <param name="keepUpright">Whether the symbol is forced to stay upright.</param>
    /// <param name="rotation">The rotation of the symbol in degrees.</param>
    [Theory]
    [InlineData(false, 45f)]
    [InlineData(true, 45f)]
    [InlineData(false, 135f)]
    [InlineData(true, 135f)]
    [InlineData(false, 225f)]
    [InlineData(true, 225f)]
    [InlineData(false, 315f)]
    [InlineData(true, 315f)]
    public void KeepUprightTest(bool keepUpright, float rotation)
    {
        var symbol = new IconPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), _sprite)
        {
            Anchor = new Point(0.0, 0.0),
            Scale = 1,
            Rotation = rotation,
            KeepUpright = keepUpright
        };

        Assert.True(DrawAndCompare(symbol, _context, $"Icon-KeepUpright-{keepUpright}-{rotation}.png"));
    }

    /// <summary>
    /// Verifies that <see cref="IconPointSymbol.Copy" /> copies all properties to a new symbol,
    /// keeping references for shared objects and delegates while copying envelopes by value.
    /// </summary>
    [Fact]
    public void CopyCopiesAllProperties()
    {
        // Arrange - fully populate all properties
        var tile = new Tile(1, 2, 3);
        var point = new Point(10, 20);
        var symbol = new IconPointSymbol(tile, 42UL, point, _sprite)
        {
            Name = "feature-name",
            StyleName = "style-name",
            SortOrder = 3.14,
            Class = "class-a",
            Subclass = "sub-a",
            Rank = 7,
            AllowOthers = true,
            Envelope = new Envelope(-5, 5, -6, 6),
            ScreenEnvelope = new Envelope(-10, 10, -10, 10),
            Native = new object(),
            Optional = true,
            AllowOverlap = true,
            Scale = 2f,
            Rotation = 45f,
            RotationAlignment = MapAlignment.Map,
            Padding = 4,
            KeepUpright = true,
            Anchor = new Point(1, 1),
            Offset = new Point(8, 16),
            ColorFilter = ctx => new float[] { 1f, 0.5f, 0.25f, 1f },
            Opacity = ctx => 0.66f,
            Translate = ctx => new Point(3, 4),
            TranslateAnchor = ctx => MapAlignment.Viewport
        };

        // Act
        var copy = symbol.Copy();

        // Assert - value and reference checks
        Assert.Same(symbol.Tile, copy.Tile);
        Assert.Equal(symbol.Id, copy.Id);
        Assert.Equal(symbol.Name, copy.Name);
        Assert.Equal(symbol.StyleName, copy.StyleName);
        Assert.Equal(symbol.SortOrder, copy.SortOrder);
        Assert.Equal(symbol.Class, copy.Class);
        Assert.Equal(symbol.Subclass, copy.Subclass);
        Assert.Equal(symbol.Rank, copy.Rank);
        Assert.Equal(symbol.AllowOthers, copy.AllowOthers);

        // Envelope and ScreenEnvelope are copied by value (new instances) in Copy()
        Assert.NotNull(symbol.Envelope);
        Assert.NotNull(copy.Envelope);
        Assert.False(ReferenceEquals(symbol.Envelope, copy.Envelope));
        Assert.Equal(symbol.Envelope.MinX, copy.Envelope.MinX);
        Assert.Equal(symbol.Envelope.MinY, copy.Envelope.MinY);
        Assert.Equal(symbol.Envelope.MaxX, copy.Envelope.MaxX);
        Assert.Equal(symbol.Envelope.MaxY, copy.Envelope.MaxY);

        Assert.NotNull(symbol.ScreenEnvelope);
        Assert.NotNull(copy.ScreenEnvelope);
        Assert.False(ReferenceEquals(symbol.ScreenEnvelope, copy.ScreenEnvelope));
        Assert.Equal(symbol.ScreenEnvelope.MinX, copy.ScreenEnvelope.MinX);
        Assert.Equal(symbol.ScreenEnvelope.MinY, copy.ScreenEnvelope.MinY);
        Assert.Equal(symbol.ScreenEnvelope.MaxX, copy.ScreenEnvelope.MaxX);
        Assert.Equal(symbol.ScreenEnvelope.MaxY, copy.ScreenEnvelope.MaxY);

        Assert.Same(symbol.Native, copy.Native);

        // Primitive properties
        Assert.Equal(symbol.Optional, copy.Optional);
        Assert.Equal(symbol.AllowOverlap, copy.AllowOverlap);
        Assert.Equal(symbol.Scale, copy.Scale);
        Assert.Equal(symbol.Rotation, copy.Rotation);
        Assert.Equal(symbol.RotationAlignment, copy.RotationAlignment);
        Assert.Equal(symbol.Padding, copy.Padding);
        Assert.Equal(symbol.KeepUpright, copy.KeepUpright);

        // Geometry-like properties (Point/Anchor/Offset)
        Assert.Same(symbol.Point, copy.Point);
        Assert.Same(symbol.Icon, copy.Icon);
        Assert.Equal(symbol.Anchor.X, copy.Anchor.X);
        Assert.Equal(symbol.Anchor.Y, copy.Anchor.Y);
        Assert.Equal(symbol.Offset.X, copy.Offset.X);
        Assert.Equal(symbol.Offset.Y, copy.Offset.Y);

        // Delegate properties - Copy preserves delegate references
        Assert.Same(symbol.ColorFilter, copy.ColorFilter);
        Assert.Same(symbol.Opacity, copy.Opacity);
        Assert.Same(symbol.Translate, copy.Translate);
        Assert.Same(symbol.TranslateAnchor, copy.TranslateAnchor);

        // Also validate delegate behavior via invocation
        var colorOrig = symbol.ColorFilter!(_context);
        var colorCopy = copy.ColorFilter!(_context);
        Assert.Equal(colorOrig.Length, colorCopy.Length);
        Assert.Equal(colorOrig, colorCopy);

        Assert.Equal(symbol.Opacity!(_context), copy.Opacity!(_context));
        var translateOrig = symbol.Translate!(_context);
        var translateCopy = copy.Translate!(_context);
        Assert.Equal(translateOrig.X, translateCopy.X);
        Assert.Equal(translateOrig.Y, translateCopy.Y);

        Assert.Equal(symbol.TranslateAnchor!(_context), copy.TranslateAnchor!(_context));
    }

    private bool DrawAndCompare(IconPointSymbol symbol, EvaluationContext context, string filename)
    {
        var paint = new SKPaint
        {
            Color = SKColors.DarkMagenta,
        };

        var tree = new Quadtree<ISymbol>();

        IconPointSymbolRenderer.CheckForSpace(_canvas, context, symbol, tree, 0, 0, false);

        if (context.Rotation != 0f)
        {
            _canvas.RotateDegrees(context.Rotation);
        }

        _paint.Color = SKColors.DarkMagenta;
        _canvas.DrawLine(new SKPoint(0, -101), new SKPoint(0, 101), _paint);
        _canvas.DrawText("N", new SKPoint(5, -89), SKTextAlign.Left, _font, _paint);
        _canvas.DrawText("S", new SKPoint(-5, 98), SKTextAlign.Right, _font, _paint);

        _paint.Color = SKColors.Magenta;
        _canvas.DrawLine(new SKPoint(-101, 0), new SKPoint(101, 0), _paint);
        _canvas.DrawText("W", new SKPoint(-98, -5), SKTextAlign.Left, _font, _paint);
        _canvas.DrawText("E", new SKPoint(97, 15), SKTextAlign.Right, _font, _paint);

        if (context.Rotation != 0f)
        {
            _canvas.RotateDegrees(-context.Rotation);
        }

        IconPointSymbolRenderer.DrawIcon(_canvas,
            context,
            symbol,
            0,
            0,
            true);

        Utilities.SaveAndDestroyBitmap(_bitmap, Path.Combine(ResultsFolder, filename));

        return Utilities.CompareBitmaps(Path.Combine(ResultsFolder, filename), Path.Combine(OriginalsFolder, filename));
    }
}
