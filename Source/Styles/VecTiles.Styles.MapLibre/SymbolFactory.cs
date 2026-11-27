using System.Collections.Concurrent;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre;

/// <summary>
/// Creates symbols for features of JSON symbol layers according to
/// their symbol placement (point or line), using cached icon and text builders
/// per layer and zoom level.
/// </summary>
public class SymbolFactory : ISymbolFactory
{
    Func<string, ISprite?> _spriteFactory;

    /// <summary>
    /// Initializes the factory with a sprite dictionary used to resolve icon names.
    /// </summary>
    /// <param name="spriteFile">The dictionary containing the sprites referenced by the styles.</param>
    /// <exception cref="ArgumentNullException">Thrown if the dictionary is null.</exception>
    public SymbolFactory(SpriteDictionary? spriteFile)
    {
        if (spriteFile == null)
            throw new ArgumentNullException(nameof(spriteFile));

        _spriteFactory = (name) =>
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (!spriteFile.Sprites.TryGetValue(name, out Sprite? sprite) && name.Contains(':'))
            {
                // Sprites of a MapLibre multi-sprite style are referenced as
                // "id:name"; fall back to the plain name when the id isn't known
                spriteFile.Sprites.TryGetValue(name.Substring(name.IndexOf(':') + 1), out sprite);
            }

            return sprite;
        };
    }

    /// <inheritdoc />
    /// <exception cref="InvalidDataException">Thrown for unsupported symbol placements.</exception>
    public ISymbol? CreateSymbol(Tile tile, ILayerStyle style, EvaluationContext context, IFeature feature)
    {
        var omtLayerStyle = (LayerStyle)style;

        var placement = omtLayerStyle.Layout.SymbolPlacement.Evaluate(context);

        if (placement == SymbolPlacement.Line && UsesViewportAlignment(omtLayerStyle, context.WithFeature(feature)))
        {
            // Per MapLibre spec, line placement with a viewport rotation
            // alignment behaves like point placement (e.g. road shields that
            // follow roads at low zooms but stay horizontal at high zooms)
            placement = SymbolPlacement.Point;
        }

        return placement switch
        {
            SymbolPlacement.Point => CreatePointSymbol(tile, omtLayerStyle, _spriteFactory, context, feature),
            SymbolPlacement.Line => CreateLineSymbol(tile, omtLayerStyle, _spriteFactory, context, feature),
            SymbolPlacement.LineCenter => CreateLineCenterSymbol(tile, omtLayerStyle, _spriteFactory, context, feature),
            _ => throw new InvalidDataException()
        };
    }

    /// <summary>
    /// Checks if the symbol would be rotated against the viewport, which makes
    /// line placement behave like point placement. Only parts that actually
    /// exist (icon image, text field) are considered; if any of them wants
    /// map alignment, line placement is kept.
    /// </summary>
    private static bool UsesViewportAlignment(LayerStyle style, EvaluationContext context)
    {
        var hasIcon = !style.Layout.IconImage.HasOnlyDefault;
        var hasText = !string.IsNullOrEmpty(style.Layout.TextField.Evaluate(context));
        var iconAlongLine = !hasIcon || style.Layout.IconRotationAlignment != MapAlignment.Viewport;
        var textAlongLine = !hasText || style.Layout.TextRotationAlignment != MapAlignment.Viewport;

        return !iconAlongLine || !textAlongLine;
    }

    // RenderedTiles are created in parallel (see OMTRenderedTileSource/GetTileAsync),
    // so access to the cached builders must be thread-safe
    ConcurrentDictionary<(string, int), IconBuilder> _iconBuilders = new();
    ConcurrentDictionary<(string, int), TextBuilder> _textBuilders = new();

    private PointSymbol? CreatePointSymbol(Tile tile, LayerStyle style, Func<string, ISprite?> spriteFactory, EvaluationContext context, IFeature feature)
    {
        // An empty geometry has no centroid or interior point: NetTopologySuite returns a
        // POINT EMPTY with NaN coordinates, which later crashes the placement of the symbol
        // (e.g. a road shield of a road that was clipped away at a high overzoom level)
        if (feature.Geometry.IsEmpty)
        {
            return null;
        }

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
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
        {
            // A non-finite symbol position must not enter the placement: the placement
            // engine would fail on it and the whole tile would stay incomplete
            return null;
        }
        var builderKey = (style.Name, (int)context.Zoom);

        var icon = _iconBuilders.GetOrAdd(builderKey,
            _ => new IconBuilder(style, spriteFactory, context)).Build(tile, point, feature);

        var text = _textBuilders.GetOrAdd(builderKey,
            _ => new TextBuilder(style, context)).Build(tile, point, feature);
        var id = feature.Attributes.Exists("id") ? (ulong)feature.Attributes["id"] : 0;

        var symbol = new PointSymbol(tile, id, point, icon, text)
        {
            Class = feature.Attributes.Exists("class") ? feature.Attributes["class"].ToString() : string.Empty,
            Subclass = feature.Attributes.Exists("subclass") ? feature.Attributes["subclass"].ToString() : string.Empty,
            // Source categories of the vector data, independent of the
            // MapLibre schema keys (e.g. basemap.de: klasse/art)
            SourceClass = AttributeValue(feature, "class", "klasse"),
            SourceSubclass = AttributeValue(feature, "subclass", "art"),
            Rank = feature.Attributes.Exists("rank") ? int.Parse(feature.Attributes["rank"].ToString()) : int.MaxValue
        };

        if (symbol is {HasIcon: false, HasText: false})
        {
            return null;
        }

        return symbol;
    }

    /// <summary>
    /// The first of the given attribute keys that the feature carries with a
    /// non-empty string value; empty when none exists. Used for the category
    /// fallback of sources that do not use the MapLibre attribute keys.
    /// </summary>
    private static string AttributeValue(IFeature feature, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (feature.Attributes?.Exists(key) != true)
            {
                continue;
            }

            var value = feature.Attributes[key]?.ToString();
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private ISymbol? CreateLineSymbol(Tile tile, LayerStyle style, Func<string, ISprite?> spriteFactory, EvaluationContext context, IFeature feature)
    {
        if (feature.Geometry.GeometryType != "LineString" || feature.Geometry.Coordinates.Length < 2)
        {
            // Symbol should be a line, but geometry isn't a line with at least 2 points
            return null;
        }

        // Convert path to world coordiantes

        var geometry = ConvertToWorldCoordinates(tile, feature.Geometry);

        if (!IsFinite(geometry))
        {
            // Features with coordinates outside the tile (e.g. clipped away paths at a high
            // overzoom level) would produce artifacts in the line symbol placement
            return null;
        }

        var builderKey = (style.Name, (int)context.Zoom);
        IconLineSymbol? icon = null;
        TextLineSymbol? text = null;

        if (!style.Layout.IconImage.HasOnlyDefault)
        {
            icon = _iconBuilders.GetOrAdd(builderKey,
                _ => new IconBuilder(style, spriteFactory, context)).Build(tile, geometry, feature);
        }

        if (!string.IsNullOrEmpty(style.Layout.TextField.Evaluate(context)))
        {
            text = _textBuilders.GetOrAdd(builderKey,
                _ => new TextBuilder(style, context)).Build(tile, geometry, feature);
        }

        var id = feature.Attributes.Exists("id") ? (ulong)feature.Attributes["id"] : 0;

        var symbol = new LineSymbol(tile, id, geometry, icon, text)
        {
            Class = feature.Attributes.Exists("class") ? feature.Attributes["class"].ToString() : string.Empty,
            Subclass = feature.Attributes.Exists("subclass") ? feature.Attributes["subclass"].ToString() : string.Empty,
            // Source categories of the vector data, independent of the
            // MapLibre schema keys (e.g. basemap.de: klasse/art)
            SourceClass = AttributeValue(feature, "class", "klasse"),
            SourceSubclass = AttributeValue(feature, "subclass", "art"),
            Rank = feature.Attributes.Exists("rank") ? int.Parse(feature.Attributes["rank"].ToString()) : int.MaxValue,
            Spacing = style.Layout.SymbolSpacing.Evaluate(context),

        };

         if (symbol is {HasIcon: false, HasText: false})
         {
             return null;
         }

         return symbol;
    }

    private static ISymbol? CreateLineCenterSymbol(Tile tile, LayerStyle style, Func<string, ISprite?> spriteFactory, EvaluationContext context, IFeature feature)
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

    private static bool IsFinite(Geometry geometry)
    {
        foreach (var coordinate in geometry.Coordinates)
        {
            if (!double.IsFinite(coordinate.X) || !double.IsFinite(coordinate.Y))
            {
                return false;
            }
        }

        return true;
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
        var worldPointY = tile.Top - (tile.Top - tile.Bottom) * pointY / 512.0;

        return (worldPointX, worldPointY);
    }
}
