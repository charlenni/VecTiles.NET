using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox;

public class MapboxSymbolFactory : ISymbolFactory
{
    Func<string, ISprite?> _spriteFactory;
    Func<string[], Topten.RichTextKit.Style?> _fontFactory;

    public MapboxSymbolFactory(MapboxSpriteFile? spriteFile)
    {
        if (spriteFile == null)
            throw new ArgumentNullException(nameof(spriteFile));

        _spriteFactory = (name) =>
        {
            if (string.IsNullOrEmpty(name) || !spriteFile.Sprites.ContainsKey(name))
            {
                return null;
            }

            var bitmap = spriteFile.Bitmap;
            var sprite = spriteFile.Sprites[name];

            if (sprite.Image == null)
            {
                if (bitmap.Native == null)
                {
                    // Convert byte array to SKImage
                    bitmap.Native = SKImage.FromEncodedData(bitmap.Binary);
                }

                sprite.Image = ((SKImage)bitmap.Native).Subset(new SKRectI(sprite.X, sprite.Y, sprite.X + sprite.Width, sprite.Y + sprite.Height));
            }

            return sprite;
        };

        _fontFactory = (names) =>
        {
            if (names == null || names.Length == 0)
            {
                return null;
            }

            foreach (var name in names)
            {
                var textStyle = CreateFont(name);

                if (textStyle != null)
                {
                    return textStyle;
                }
            }

            return null;
        };
    }

    public ISymbol? CreateSymbol(Tile tile, ILayerStyle style, EvaluationContext context, IFeature feature)
    {
        var mapboxLayerStyle = (MapboxLayerStyle)style;

        return mapboxLayerStyle.Layout.SymbolPlacement.Evaluate(context) switch
        {
            SymbolPlacement.Point => CreatePointSymbol(tile, mapboxLayerStyle, _spriteFactory, _fontFactory, context, feature),
            SymbolPlacement.Line => CreateLineSymbol(tile, mapboxLayerStyle, _spriteFactory, _fontFactory, context, feature),
            SymbolPlacement.LineCenter => CreateLineCenterSymbol(tile, mapboxLayerStyle, _spriteFactory, context, feature),
            _ => throw new InvalidDataException()
        };
    }

    Dictionary<(string, int), MapboxIconBuilder> _iconBuilders = new();
    Dictionary<(string, int), MapboxTextBuilder> _textBuilders = new();

    private MapboxPointSymbol? CreatePointSymbol(Tile tile, MapboxLayerStyle style, Func<string, ISprite?> spriteFactory, Func<string[], Topten.RichTextKit.Style> fontFactory, EvaluationContext context, IFeature feature)
    {
        var geometryPoint = feature.Geometry.Centroid;

        if (feature.Geometry.GeometryType != "Point" || feature.Geometry.Coordinates.Length != 1)
        {
            // Symbol should be a point, but geometry isn't a point, so use an interior point instead
            geometryPoint = feature.Geometry.InteriorPoint;
        }

        /*if (feature.Geometry.Coordinate.X < 0 || feature.Geometry.Coordinate.X >= 512 ||
            feature.Geometry.Coordinate.Y < 0 || feature.Geometry.Coordinate.Y >= 512)
        {
            // TODO
            // Check, why this happens. It seems, that there are really points in
            // the tile, that don't belong to this tile.
            // Symbol should be inside of the tile
            return null;
        }*/

        var point = ConvertFromLatLon(tile, geometryPoint);
        var builderKey = (style.Name, (int)context.Zoom);

        if (!_iconBuilders.ContainsKey(builderKey))
        {
            _iconBuilders[builderKey] = new MapboxIconBuilder(style, spriteFactory, context);
        }

        var icon = _iconBuilders[builderKey].Build(tile, point, feature);

        if (!_textBuilders.ContainsKey(builderKey))
        {
            _textBuilders[builderKey] = new MapboxTextBuilder(style, fontFactory, context);
        }

        var text = _textBuilders[builderKey].Build(tile, point, feature);

        var symbol = new MapboxPointSymbol(tile, point, icon, text)
        {
            Class = feature.Attributes.Exists("class") ? feature.Attributes["class"].ToString() : string.Empty,
            Subclass = feature.Attributes.Exists("subclass") ? feature.Attributes["subclass"].ToString() : string.Empty,
            Rank = feature.Attributes.Exists("rank") ? int.Parse(feature.Attributes["rank"].ToString()) : int.MaxValue
        };

        if (!symbol.HasIcon && !symbol.HasText)
        {
            return null;
        }

        return symbol;
    }

    private ISymbol? CreateLineSymbol(Tile tile, MapboxLayerStyle style, Func<string, ISprite?> spriteFactory, Func<string[], Topten.RichTextKit.Style> fontFactory, EvaluationContext context, IFeature feature)
    {
        if (feature.Geometry.GeometryType != "LineString" || feature.Geometry.Coordinates.Length < 2)
        {
            // Symbol should be a line, but geometry isn't a line with at least 2 points
            return null;
        }

        // Convert path to world coordiantes

        var geometry = ConvertToWorldCoordinates(tile, feature.Geometry);

        MapboxSymbol? symbol = null;

        var builderKey = (style.Name, (int)context.Zoom);

        if (!style.Layout.IconImage.HasOnlyDefault)
        {
            if (!_iconBuilders.ContainsKey(builderKey))
            {
                _iconBuilders[builderKey] = new MapboxIconBuilder(style, spriteFactory, context);
            }

            symbol = _iconBuilders[builderKey].Build(tile, geometry, feature);
        }

        if (!string.IsNullOrEmpty(style.Layout.TextField))
        {
            if (!_textBuilders.ContainsKey(builderKey))
            {
                _textBuilders[builderKey] = new MapboxTextBuilder(style, fontFactory, context);
            }

            //symbol = _textBuilders[builderKey].Build(tile, geometry, feature);
        }

        if (symbol == null)
        {
            return null;
        }

        symbol.Class = feature.Attributes.Exists("class") ? feature.Attributes["class"].ToString() : string.Empty;
        symbol.Subclass = feature.Attributes.Exists("subclass") ? feature.Attributes["subclass"].ToString() : string.Empty;
        symbol.Rank = feature.Attributes.Exists("rank") ? int.Parse(feature.Attributes["rank"].ToString()) : int.MaxValue;

        return symbol;
    }

    private static ISymbol? CreateLineCenterSymbol(Tile tile, MapboxLayerStyle style, Func<string, ISprite?> spriteFactory, EvaluationContext context, IFeature feature)
    {
        // TODO
        return null;
    }

    private static Dictionary<string, Topten.RichTextKit.Style> _textStyles = new();

    private class CoordinateTransformer : ICoordinateSequenceFilter
    {
        private Tile _tile;

        public bool Done => false;
        public bool GeometryChanged => true;

        public CoordinateTransformer(Tile tile)
        {
            _tile = tile;
        }

        public void Filter(CoordinateSequence seq, int i)
        {
            var x = seq.GetX(i);
            var y = seq.GetY(i);

            (var worldX, var worldY) = ConvertFromLatLon(_tile, x, y);

            seq.SetX(i, worldX);
            seq.SetY(i, worldY);
        }
    }

    private Geometry ConvertToWorldCoordinates(Tile tile, Geometry geometry)
    {
        var result = geometry.Copy();

        result.Apply(new CoordinateTransformer(tile));

        return result;
    }

    private static Topten.RichTextKit.Style? CreateFont(string fontName)
    {
        if (string.IsNullOrEmpty(fontName))
        {
            return null;
        }

        if (_textStyles.TryGetValue(fontName, out var textStyle))
        {
            return textStyle;
        }

        textStyle = new Topten.RichTextKit.Style();

        // TODO: Create correct family name
        var fontFamilyName = fontName;

        if (fontFamilyName.Contains("condensed", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontWidth = SKFontStyleWidth.Condensed;
            fontFamilyName = fontFamilyName.Replace("condensed", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        textStyle.FontWeight = 400;

        if (fontFamilyName.Contains("regular", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontWeight = 400;
            fontFamilyName = fontFamilyName.Replace("regular", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        if (fontFamilyName.Contains("medium", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontWeight = 500;
            fontFamilyName = fontFamilyName.Replace("medium", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        if (fontFamilyName.Contains("bold", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontWeight = 500;
            fontFamilyName = fontFamilyName.Replace("bold", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        if (fontFamilyName.Contains("italic", System.StringComparison.InvariantCultureIgnoreCase))
        {
            textStyle.FontItalic = true;
            fontFamilyName = fontFamilyName.Replace("italic", "", System.StringComparison.InvariantCultureIgnoreCase);
        }

        fontFamilyName = fontFamilyName.Replace("  ", " ").Trim();

        var fontManager = SKFontManager.Default;
        var typeface = fontManager.MatchFamily(fontFamilyName);

        // Prüfen, ob die Rückgabe tatsächlich die gewünschte Familie ist
        bool fontExists = typeface != null && typeface.FamilyName.Equals(fontFamilyName, StringComparison.OrdinalIgnoreCase);

        if (!fontExists)
        {
            return null;
        }

        textStyle.FontFamily = fontFamilyName;

        _textStyles.Add(fontName, textStyle);

        return textStyle;
    }

    private static Point ConvertFromLatLon(Tile tile, Point point)
    {
        (var x, var y) = ConvertFromLatLon(tile, point.X, point.Y);

        return new Point(x, y);
    }

    private static (double, double) ConvertFromLatLon(Tile tile, double pointX, double pointY)
    {
        const double radius = 6378137.0;
        const double pi180 = Math.PI / 180.0;
        const double pi360 = Math.PI / 360.0;
        const double pi4 = Math.PI / 4.0;

        double LonToX(double lon) => radius * pi180 * lon;
        double LatToY(double lat) => radius * Math.Log(Math.Tan(pi4 + pi360 * lat));

        double left = LonToX(tile.Left);
        double bottom = LatToY(tile.Bottom);
        double right = LonToX(tile.Right);
        double top = LatToY(tile.Top);

        var worldPointX = left + (right - left) * pointX / 512.0;
        var worldPointY = top - (top - bottom) * pointY / 512.0;

        return (worldPointX, worldPointY);
    }
}
