using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using SkiaSharp;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Renderers.Skia.Primitives;
using Color = VecTiles.Common.Primitives.Color;
using TextAlignment = Topten.RichTextKit.TextAlignment;

namespace VecTiles.Renderers.Skia.Test;

public class TextPointSymbolTests
{
    private static readonly string _path = Utilities.DataFiles;
    
    private readonly string OriginalsFolder = Path.Combine(Utilities.TestFiles, "Originals");
    private readonly string ResultsFolder = Path.Combine(Utilities.TestFiles, "Results");
    private SKBitmap _bitmap;
    private SKCanvas _canvas;
    private SKPaint _paint = new SKPaint();
    private SKFont _font = new SKFont(SKTypeface.FromFamilyName("Roboto", SKFontStyle.Bold));
    private EvaluationContext _context = new EvaluationContext(0, 1, 0);

    public TextPointSymbolTests()
    {
        _bitmap = Utilities.CreateBitmap(201, 201);
        _canvas = new SKCanvas(_bitmap);

        // Prepare canvas
        _canvas.Clear(SKColors.LightGray);
        _canvas.Translate(100, 100);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(20.0, 0.0)]
    [InlineData(0.0, 20.0)]
    public void OffsetTest(double x, double y)
    {
        var symbol = new TextPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), "Text")
        {
            Anchor = new Point(0, 0),
            Offset = new Point(x, y),
            Padding = 0,
            FontNames = new[] { "Roboto" },
            FontSize = ctx => 12,
            MaxWidth = (ctx, fs) => 200
        };

        Assert.True(DrawAndCompare(symbol, _context, $"Text-Offset-{x}x-{y}y.png"));
    }

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
    public void AnchorTest(double x, double y)
    {
        var symbol = new TextPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), "Anchor")
        {
            Anchor = new Point(x, y),
            Padding = 0,
            FontNames = new[] { "Roboto" },
            FontSize = ctx => 12,
            MaxWidth = (ctx, fs) => 200
        };

        Assert.True(DrawAndCompare(symbol, _context, $"Text-Anchor-{x * 100}x-{y * 100}y.png"));
    }

    [Fact]
    public void OpacityTest()
    {
        var tree = new Quadtree<ISymbol>();

        OpacityDrawTest(1f, tree, 0, 0);
        OpacityDrawTest(0.9f, tree, 100, 0);
        OpacityDrawTest(0.8f, tree, 0, 45);
        OpacityDrawTest(0.7f, tree, 100, 45);
        OpacityDrawTest(0.6f, tree, 0, 90);
        OpacityDrawTest(0.5f, tree, 100, 90);
        OpacityDrawTest(0.4f, tree, 0, 135);
        OpacityDrawTest(0.3f, tree, 100, 135);
        OpacityDrawTest(0.2f, tree, 0, 180);
        OpacityDrawTest(0.1f, tree, 100, 180);

        var filename = $"Text-Opacity.png";

        Utilities.SaveAndDestroyBitmap(_bitmap, Path.Combine(ResultsFolder, filename));

        Assert.True(Utilities.CompareBitmaps(Path.Combine(ResultsFolder, filename), Path.Combine(OriginalsFolder, filename)));
    }

    private void OpacityDrawTest(float opacity, Quadtree<ISymbol> tree, float x, float y)
    {
        var symbol = new TextPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), "Opacity")
        {
            Anchor = new Point(0, 0),
            Opacity = ctx => opacity,
            Color = ctx => new Color(0, 0, 0, 1),
            Padding = 0,
            FontNames = new[] { "Roboto" },
            FontSize = ctx => 12,
            MaxWidth = (ctx, fs) => 200
        };

        _canvas.Save();
        _canvas.Translate(-100, -100);

        if (TextPointSymbolRenderer.CheckForSpace(_canvas, _context, symbol, tree, x, y, 0, false) == SymbolPlacementStatus.Success)
        {
            TextPointSymbolRenderer.DrawText(_canvas, _context, symbol, x, y, 0, true);
        }

        _canvas.Restore();
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(10.0, 0.0)]
    [InlineData(0.0, 10.0)]
    public void TranslateTest(double tx, double ty)
    {
        var symbol = new TextPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), "Translate")
        {
            Anchor = new Point(0, 0),
            Translate = ctx => new Point(tx, ty),
            TranslateAnchor = ctx => MapAlignment.Map,
            Padding = 0,
            FontNames = new[] { "Roboto" },
            FontSize = ctx => 12,
            MaxWidth = (ctx, fs) => 200
        };

        Assert.True(DrawAndCompare(symbol, _context, $"Text-Translate-{tx}x-{ty}y.png"));
    }

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
        var symbol = new TextPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), "TAnchor")
        {
            Anchor = new Point(0, 0),
            RotationAlignment = rotationAlignment,
            Translate = ctx => new Point(30, 10),
            TranslateAnchor = ctx => anchor,
            Padding = 0,
            FontNames = new[] { "Roboto" },
            FontSize = ctx => 12,
            MaxWidth = (ctx, fs) => 200
        };

        Assert.True(DrawAndCompare(symbol, rotationContext, $"Text-TranslateAnchor-{anchor}-Alignment-{rotationAlignment}.png"));
    }

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
        var symbol = new TextPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), "Rotation")
        {
            Anchor = new Point(0, 0),
            Rotation = (float)rotation,
            RotationAlignment = MapAlignment.Viewport,
            Padding = 0,
            FontNames = new[] { "Roboto" },
            FontSize = ctx => 12,
            MaxWidth = (ctx, fs) => 200
        };

        Assert.True(DrawAndCompare(symbol, _context, $"Text-Rotation-{rotation}.png"));
    }

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
        var symbol = new TextPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), "RotAlign")
        {
            Anchor = new Point(0, 0),
            Rotation = rotation,
            RotationAlignment = align,
            Padding = 0,
            FontNames = new[] { "Roboto" },
            FontSize = ctx => 12,
            MaxWidth = (ctx, fs) => 200
        };

        Assert.True(DrawAndCompare(symbol, rotationContext, $"Text-RotationAlignment-{align}-{rotation}-{mapRotation}.png"));
    }

    [Fact]
    public void PaddingTest()
    {
        var tree = new Quadtree<ISymbol>();

        PaddingDrawingTest(0, tree, 20, 5);
        PaddingDrawingTest(1, tree, 100, 5);
        PaddingDrawingTest(2, tree, 20, 45);
        PaddingDrawingTest(3, tree, 100, 45);
        PaddingDrawingTest(4, tree, 20, 85);
        PaddingDrawingTest(5, tree, 100, 85);
        PaddingDrawingTest(6, tree, 20, 125);
        PaddingDrawingTest(8, tree, 100, 125);
        PaddingDrawingTest(10, tree, 20, 165);
        PaddingDrawingTest(15, tree, 100, 165);

        var filename = $"Text-Padding.png";

        Utilities.SaveAndDestroyBitmap(_bitmap, Path.Combine(ResultsFolder, filename));

        Assert.True(Utilities.CompareBitmaps(Path.Combine(ResultsFolder, filename), Path.Combine(OriginalsFolder, filename)));
    }

    private void PaddingDrawingTest(int padding, Quadtree<ISymbol> tree, float x, float y)
    {
        var symbol = new TextPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), "Padding")
        {
            Anchor = new Point(0, 0),
            Padding = padding,
            FontNames = new[] { "Roboto" },
            FontSize = ctx => 16,
            MaxWidth = (ctx, fs) => 200
        };

        _canvas.Save();
        _canvas.Translate(-100, -100);

        if (TextPointSymbolRenderer.CheckForSpace(_canvas, _context, symbol, tree, x, y, 0, false) == SymbolPlacementStatus.Success)
        {
            TextPointSymbolRenderer.DrawText(_canvas, _context, symbol, x, y, 0, true);
        }

        _canvas.Restore();
    }

    [Fact]
    public void FontTest()
    {
        var tree = new Quadtree<ISymbol>();

        FontDrawingTest(new[] { "Roboto" }, "Roboto", tree, 10, 5);
        FontDrawingTest(new[] { "Roboto Italic" }, "Roboto Italic", tree, 100, 5);
        FontDrawingTest(new[] { "Roboto Bold" }, "Roboto Bold", tree, 10, 40);
        FontDrawingTest(new[] { "Roboto Condensed" }, "Roboto Condensed", tree, 100, 40);
        FontDrawingTest(new[] { "Roboto Condensed Italic" }, "Roboto Condensed Italic", tree, 10, 75);
        FontDrawingTest(new[] { "Roboto Regular" }, "Roboto Regular", tree, 10, 110);
//        FontDrawingTest(new[] { "Arial" }, "Arial", tree, 100, 110);
//        FontDrawingTest(new[] { "Arial Italic" }, "Arial Italic", tree, 10, 145);
//        FontDrawingTest(new[] { "Arial Bold" }, "Arial Bold", tree, 100, 145);
//        FontDrawingTest(new[] { "Microsoft YaHei" }, "测试字体", tree, 10, 180);

        var filename = $"Text-Font.png";

        Utilities.SaveAndDestroyBitmap(_bitmap, Path.Combine(ResultsFolder, filename));

        Assert.True(Utilities.CompareBitmaps(Path.Combine(ResultsFolder, filename), Path.Combine(OriginalsFolder, filename)));
    }

    private void FontDrawingTest(string[] fonts, string text, Quadtree<ISymbol> tree, float x, float y)
    {
        var symbol = new TextPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), text)
        {
            Anchor = new Point(0, 0),
            FontNames = fonts,
            FontSize = ctx => 12,
            MaxWidth = (ctx, fs) => 200
        };

        _canvas.Save();
        _canvas.Translate(-100, -100);

        if (TextPointSymbolRenderer.CheckForSpace(_canvas, _context, symbol, tree, x, y, 0, false) == SymbolPlacementStatus.Success)
        {
            TextPointSymbolRenderer.DrawText(_canvas, _context, symbol, x, y, 0, true);
        }

        _canvas.Restore();
    }

    [Theory]
    [InlineData(false, 0f)]
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
        var symbol = new TextPointSymbol(new Tile(0, 0, 0), 0, new Point(0, 0), "KeepUpright")
        {
            Anchor = new Point(0.5, 0.5),
            Rotation = rotation,
            KeepUpright = keepUpright,
            Padding = 0,
            FontNames = new[] { "Roboto" },
            FontSize = ctx => 12,
            MaxWidth = (ctx, fs) => 200
        };

        Assert.True(DrawAndCompare(symbol, _context, $"Text-KeepUpright-{keepUpright}-{rotation}.png"));
    }

    [Fact]
    public void CopyCopiesAllProperties()
    {
        var tile = new Tile(1, 2, 3);
        var point = new Point(10, 20);
        var symbol = new TextPointSymbol(tile, 42UL, point, "CopyText")
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
            Rotation = 45f,
            RotationAlignment = MapAlignment.Map,
            Padding = 4,
            KeepUpright = true,
            Anchor = new Point(1, 1),
            Offset = new Point(8, 16),
            Color = ctx => new Color(255, 127, 63, 255),
            Opacity = ctx => 0.66f,
            Alignment = TextJustify.Center,
            Direction = TextDirection.Auto,
            FontNames = new[] { "SansSerif" },
            FontSize = ctx => 14,
            HaloColor = ctx => new Color(0, 0, 0, 1),
            HaloBlur = ctx => 1f,
            HaloWidth = ctx => 1f,
            Translate = ctx => new Point(3, 4),
            TranslateAnchor = ctx => MapAlignment.Viewport,
            MaxWidth = (ctx, fs) => 120
        };

        var copy = symbol.Copy();

        Assert.Same(symbol.Tile, copy.Tile);
        Assert.Equal(symbol.Id, copy.Id);
        Assert.Equal(symbol.Name, copy.Name);
        Assert.Equal(symbol.StyleName, copy.StyleName);
        Assert.Equal(symbol.SortOrder, copy.SortOrder);
        Assert.Equal(symbol.Class, copy.Class);
        Assert.Equal(symbol.Subclass, copy.Subclass);
        Assert.Equal(symbol.Rank, copy.Rank);
        Assert.Equal(symbol.AllowOthers, copy.AllowOthers);

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

        Assert.Equal(symbol.Optional, copy.Optional);
        Assert.Equal(symbol.AllowOverlap, copy.AllowOverlap);
        Assert.Equal(symbol.Rotation, copy.Rotation);
        Assert.Equal(symbol.RotationAlignment, copy.RotationAlignment);
        Assert.Equal(symbol.Padding, copy.Padding);
        Assert.Equal(symbol.KeepUpright, copy.KeepUpright);

        Assert.Same(symbol.Point, copy.Point);
        Assert.Equal(symbol.Anchor.X, copy.Anchor.X);
        Assert.Equal(symbol.Anchor.Y, copy.Anchor.Y);
        Assert.Equal(symbol.Offset.X, copy.Offset.X);
        Assert.Equal(symbol.Offset.Y, copy.Offset.Y);

        Assert.Same(symbol.Color, copy.Color);
        Assert.Same(symbol.Opacity, copy.Opacity);
        Assert.Same(symbol.FontSize, copy.FontSize);
        Assert.Same(symbol.HaloColor, copy.HaloColor);
        Assert.Same(symbol.HaloBlur, copy.HaloBlur);
        Assert.Same(symbol.HaloWidth, copy.HaloWidth);
        Assert.Same(symbol.Translate, copy.Translate);
        Assert.Same(symbol.TranslateAnchor, copy.TranslateAnchor);
        Assert.Same(symbol.MaxWidth, copy.MaxWidth);

        // Validate delegate behavior
        Assert.Equal(symbol.Color!(_context), copy.Color!(_context));
        Assert.Equal(symbol.Opacity!(_context), copy.Opacity!(_context));
        Assert.Equal(symbol.FontSize!(_context), copy.FontSize!(_context));
        Assert.Equal(symbol.HaloBlur!(_context), copy.HaloBlur!(_context));
        Assert.Equal(symbol.HaloWidth!(_context), copy.HaloWidth!(_context));
        Assert.Equal(symbol.Translate!(_context).X, copy.Translate!(_context).X);
        Assert.Equal(symbol.Translate!(_context).Y, copy.Translate!(_context).Y);
        Assert.Equal(symbol.TranslateAnchor!(_context), copy.TranslateAnchor!(_context));
        Assert.Equal(symbol.MaxWidth!(_context, symbol.FontSize!(_context)), copy.MaxWidth!(_context, copy.FontSize!(_context)));
    }

    private bool DrawAndCompare(TextPointSymbol symbol, EvaluationContext context, string filename)
    {
        var tree = new Quadtree<ISymbol>();

        // Compute rotation used for envelope and drawing (map alignment includes context rotation)
        var totalRotation = symbol.Rotation;
        if (symbol.RotationAlignment == MapAlignment.Map)
        {
            totalRotation += context.Rotation;
        }

        TextPointSymbolRenderer.CheckForSpace(_canvas, context, symbol, tree, 0, 0, totalRotation, false);

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

        TextPointSymbolRenderer.DrawText(_canvas,
            context,
            symbol,
            0,
            0,
            totalRotation,
            true);

        Utilities.SaveAndDestroyBitmap(_bitmap, Path.Combine(ResultsFolder, filename));

        return Utilities.CompareBitmaps(Path.Combine(ResultsFolder, filename), Path.Combine(OriginalsFolder, filename));
    }

    public class TextLetterSpacingTests
    {
        [Fact]
        public void SpacingWidensText()
        {
            const float fontSize = 14.4f;

            var withSpacing = RenderedTextCache.GetOrCreate("Hermetschwil", "test", new[] { "Roboto" },
                fontSize, 1.2f, 0.1f, fontSize * 8, TextAlignment.Left, 0);
            var withoutSpacing = RenderedTextCache.GetOrCreate("Hermetschwil", "test", new[] { "Roboto" },
                fontSize, 1.2f, 0f, fontSize * 8, TextAlignment.Left, 0);

            Assert.Equal(1, withSpacing.Text.LineCount);
            Assert.Equal(1, withoutSpacing.Text.LineCount);
            Assert.True(withSpacing.MeasuredWidth > withoutSpacing.MeasuredWidth,
                $"spaced width {withSpacing.MeasuredWidth} should exceed unspaced width {withoutSpacing.MeasuredWidth}");
        }
    }
}
