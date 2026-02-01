using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles;

public class OMTSymbolFactory : ISymbolFactory
{
    Func<string, ISprite?> _spriteFactory;

    public OMTSymbolFactory(SpriteDicionary? spriteFile)
    {
        if (spriteFile == null)
            throw new ArgumentNullException(nameof(spriteFile));

        _spriteFactory = (name) =>
        {
            if (string.IsNullOrEmpty(name) || !spriteFile.Sprites.TryGetValue(name, out Sprite? sprite))
            {
                return null;
            }

            return sprite;
        };
    }

    public ISymbol? CreateSymbol(Tile tile, ILayerStyle style, EvaluationContext context, IFeature feature)
    {
        var omtLayerStyle = (OMTLayerStyle)style;

        return omtLayerStyle.Layout.SymbolPlacement.Evaluate(context) switch
        {
            SymbolPlacement.Point => CreatePointSymbol(tile, omtLayerStyle, _spriteFactory, context, feature),
            SymbolPlacement.Line => CreateLineSymbol(tile, omtLayerStyle, _spriteFactory, context, feature),
            SymbolPlacement.LineCenter => CreateLineCenterSymbol(tile, omtLayerStyle, _spriteFactory, context, feature),
            _ => throw new InvalidDataException()
        };
    }

    Dictionary<(string, int), OMTIconBuilder> _iconBuilders = new();
    Dictionary<(string, int), OMTTextBuilder> _textBuilders = new();

    private PointSymbol? CreatePointSymbol(Tile tile, OMTLayerStyle style, Func<string, ISprite?> spriteFactory, EvaluationContext context, IFeature feature)
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

        var point = ConvertFrom(tile, geometryPoint);
        var builderKey = (style.Name, (int)context.Zoom);

        if (!_iconBuilders.ContainsKey(builderKey))
        {
            _iconBuilders[builderKey] = new OMTIconBuilder(style, spriteFactory, context);
        }

        var icon = _iconBuilders[builderKey].Build(tile, point, feature);

        if (!_textBuilders.ContainsKey(builderKey))
        {
            _textBuilders[builderKey] = new OMTTextBuilder(style, context);
        }

        var text = _textBuilders[builderKey].Build(tile, point, feature);

        var symbol = new PointSymbol(tile, point, icon, text)
        {
            Class = feature.Attributes.Exists("class") ? feature.Attributes["class"].ToString() : string.Empty,
            Subclass = feature.Attributes.Exists("subclass") ? feature.Attributes["subclass"].ToString() : string.Empty,
            Rank = feature.Attributes.Exists("rank") ? int.Parse(feature.Attributes["rank"].ToString()) : int.MaxValue
        };

        if (symbol is {HasIcon: false, HasText: false})
        {
            return null;
        }

        return symbol;
    }

    private ISymbol? CreateLineSymbol(Tile tile, OMTLayerStyle style, Func<string, ISprite?> spriteFactory, EvaluationContext context, IFeature feature)
    {
        if (feature.Geometry.GeometryType != "LineString" || feature.Geometry.Coordinates.Length < 2)
        {
            // Symbol should be a line, but geometry isn't a line with at least 2 points
            return null;
        }

        // Convert path to world coordiantes

        var geometry = ConvertToWorldCoordinates(tile, feature.Geometry);

        Symbol? symbol = null;

        var builderKey = (style.Name, (int)context.Zoom);

        if (!style.Layout.IconImage.HasOnlyDefault)
        {
            if (!_iconBuilders.ContainsKey(builderKey))
            {
                _iconBuilders[builderKey] = new OMTIconBuilder(style, spriteFactory, context);
            }

            symbol = _iconBuilders[builderKey].Build(tile, geometry, feature);
        }

        if (!string.IsNullOrEmpty(style.Layout.TextField))
        {
            if (!_textBuilders.ContainsKey(builderKey))
            {
                _textBuilders[builderKey] = new OMTTextBuilder(style, context);
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

    private static ISymbol? CreateLineCenterSymbol(Tile tile, OMTLayerStyle style, Func<string, ISprite?> spriteFactory, EvaluationContext context, IFeature feature)
    {
        // TODO
        return null;
    }

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

            (var worldX, var worldY) = ConvertFrom(_tile, x, y);

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

    private static Point ConvertFrom(Tile tile, Point point)
    {
        (var x, var y) = ConvertFrom(tile, point.X, point.Y);

        return new Point(x, y);
    }

    private static (double, double) ConvertFrom(Tile tile, double pointX, double pointY)
    {
        double left = tile.Left;
        double bottom = tile.Bottom;
        double right = tile.Right;
        double top = tile.Top;

        var worldPointX = tile.Left + (tile.Right - tile.Left) * pointX / 512.0;
        var worldPointY = tile.Top + (tile.Bottom - tile.Top) * pointY / 512.0;

        return (worldPointX, worldPointY);
    }
}
