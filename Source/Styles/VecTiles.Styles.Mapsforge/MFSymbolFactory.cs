using System.Globalization;
using NetTopologySuite.Features;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapsforge.Enums;
using NtsPoint = NetTopologySuite.Geometries.Point;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Creates symbols for the symbol layers of a Mapsforge render theme: icons
/// and captions at point features (symbol/caption), icons repeated along lines
/// (lineSymbol) and labels along paths (pathText). Icons are resolved as
/// sprites (raster or vector) through the sprite factory of the theme.
/// </summary>
public class MFSymbolFactory : ISymbolFactory
{
    private readonly MFSpriteFactory _spriteFactory;

    /// <summary>
    /// Initializes the factory with the sprite factory of its render theme.
    /// </summary>
    /// <param name="spriteFactory">Factory that resolves icon sources into sprites.</param>
    public MFSymbolFactory(MFSpriteFactory spriteFactory)
    {
        _spriteFactory = spriteFactory;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown for an unknown layer style type.</exception>
    public ISymbol? CreateSymbol(Tile tile, ILayerStyle style, EvaluationContext context, IFeature feature)
    {
        return style switch
        {
            MFSymbolLayerStyle symbolStyle => CreatePointIconSymbol(tile, symbolStyle, feature),
            MFCaptionLayerStyle captionStyle => CreateCaptionSymbol(tile, captionStyle, feature),
            MFLineSymbolLayerStyle lineSymbolStyle => CreateLineIconSymbol(tile, lineSymbolStyle, feature),
            MFPathTextLayerStyle pathTextStyle => CreatePathTextSymbol(tile, pathTextStyle, feature),
            _ => throw new ArgumentException($"Layer style with type '{style.StyleType}' is not supported by Mapsforge render themes")
        };
    }

    /// <summary>
    /// Creates the icon point symbol of a symbol layer at a point feature.
    /// </summary>
    private ISymbol? CreatePointIconSymbol(Tile tile, MFSymbolLayerStyle style, IFeature feature)
    {
        var (allowOverlap, allowOthers) = GetDisplayBehavior(style.Display);
        if (allowOverlap < 0)
        {
            // Display directive "never": the layer is not rendered
            return null;
        }

        var sprite = _spriteFactory.GetSprite(style.Src, style.SymbolWidth, style.SymbolHeight, style.SymbolPercent);
        if (sprite == null)
        {
            // A missing icon must not prevent the theme from rendering
            return null;
        }

        var point = GetFeaturePoint(tile, feature);
        if (point == null)
        {
            return null;
        }

        var (anchor, offset) = GetPointAnchor(style.Position);

        var icon = new IconPointSymbol(tile, GetFeatureId(feature), point, sprite)
        {
            Name = GetFeatureName(feature),
            StyleName = style.Name,
            Optional = true,
            AllowOverlap = allowOverlap > 0,
            AllowOthers = allowOthers,
            // Icons of a higher priority are placed first (lower sort order)
            SortOrder = -style.Priority,
            // Icons stay upright regardless of the map rotation
            RotationAlignment = MapAlignment.Viewport,
            KeepUpright = true,
            Anchor = anchor,
            Offset = offset,
            // The sprite already carries the display size of the icon
            // (symbol-width / symbol-percent / mapsforge default size), so
            // the draw scale multiplier is 1. Without it the icon collapses
            // to a point (IconPointSymbolRenderer scales by symbol.Scale).
            Scale = 1f
        };

        // The placement and draw paths accept point symbols only as
        // PointSymbol envelopes (see SymbolPlacementEngine / PointSymbolRenderer):
        // a bare IconPointSymbol is quietly skipped, so the icon is wrapped
        // like the OpenMapTiles symbol factory does
        return PointSymbolOf(icon);
    }

    /// <summary>
    /// Creates the text point symbol of a caption layer at a point feature.
    /// </summary>
    private ISymbol? CreateCaptionSymbol(Tile tile, MFCaptionLayerStyle style, IFeature feature)
    {
        var (allowOverlap, allowOthers) = GetDisplayBehavior(style.Display);
        if (allowOverlap < 0)
        {
            // Display directive "never": the layer is not rendered
            return null;
        }

        var caption = GetText(feature, style.Key, style.TextTransform);
        if (string.IsNullOrEmpty(caption))
        {
            return null;
        }

        var point = GetFeaturePoint(tile, feature);
        if (point == null)
        {
            return null;
        }

        var (anchor, offset) = GetCaptionAnchor(style);

        var text = new TextPointSymbol(tile, GetFeatureId(feature), point, caption)
        {
            Name = GetFeatureName(feature),
            StyleName = style.Name,
            Optional = true,
            AllowOverlap = allowOverlap > 0,
            AllowOthers = allowOthers,
            // Captions of a higher priority are placed first (lower sort order)
            SortOrder = -style.Priority,
            RotationAlignment = MapAlignment.Viewport,
            KeepUpright = true,
            Anchor = anchor,
            Offset = offset,
            Color = (context) => style.Fill,
            FontNames = GetFontNames(style.FontFamily, style.FontStyle),
            FontSize = (context) => style.FontSize * style.BaseTextSize,
            HaloColor = (context) => style.Stroke,
            HaloWidth = (context) => style.StrokeWidth * style.BaseStrokeWidth,
            // The Mapsforge caption stroke-width is a painted stroke width, not an outward
            // distance like the OMT halo-width (renderer compensation, see TextPointSymbol)
            // Mapsforge captions of area features (polygons): the clipped
            // copies of the same polygon in neighbouring tiles must be
            // deduplicated per text in the placement pass (see
            // TextPointSymbol.IsAreaCaption), since without feature ids
            // nothing else identifies the copies of one polygon
            IsAreaCaption = feature.Geometry.GeometryType is "Polygon" or "MultiPolygon",
            HaloIsOutsideDistance = false,
            MaxWidth = style.TextWrapWidth.HasValue
                ? (context, fontSize) => style.TextWrapWidth.Value * fontSize
                : null
        };

        // The placement and draw paths accept point symbols only as
        // PointSymbol envelopes (see SymbolPlacementEngine / PointSymbolRenderer):
        // a bare TextPointSymbol is quietly skipped, so the caption is wrapped
        // like the OpenMapTiles symbol factory does
        return PointSymbolOf(text);
    }

    /// <summary>
    /// Creates the icon line symbol of a lineSymbol layer: an icon placed at
    /// the center of the way or repeated along it.
    /// </summary>
    private ISymbol? CreateLineIconSymbol(Tile tile, MFLineSymbolLayerStyle style, IFeature feature)
    {
        var (allowOverlap, allowOthers) = GetDisplayBehavior(style.Display);
        if (allowOverlap < 0)
        {
            // Display directive "never": the layer is not rendered
            return null;
        }

        var sprite = _spriteFactory.GetSprite(style.Src, style.SymbolWidth, style.SymbolHeight, style.SymbolPercent);
        if (sprite == null)
        {
            // A missing icon must not prevent the theme from rendering
            return null;
        }

        var geometry = GetFeatureLineGeometry(tile, feature);
        if (geometry == null)
        {
            return null;
        }

        var orientationRotation = GetOrientationRotation(style.Orientation);

        var iconLineSymbol = new IconLineSymbol(tile, GetFeatureId(feature), geometry, sprite)
        {
            Name = GetFeatureName(feature),
            StyleName = style.Name,
            Optional = true,
            AllowOverlap = allowOverlap > 0,
            AllowOthers = allowOthers,
            // Symbols of a higher priority are placed first (lower sort order)
            SortOrder = -style.Priority,
            Rotation = orientationRotation,
            // With rotate, the icon follows the line direction, otherwise it
            // keeps its orientation regardless of the path
            RotationAlignment = style.Rotate ? MapAlignment.Map : MapAlignment.Viewport,
            KeepUpright = !style.Rotate,
            Anchor = new NtsPoint(0.5, 0.5),
            // Vertical offset of the icon relative to the line (perpendicular)
            Offset = new NtsPoint(0, style.Dy)
        };

        return new LineSymbol(tile, GetFeatureId(feature), geometry,
            iconLineSymbol, text: null)
        {
            Name = GetFeatureName(feature),
            StyleName = style.Name,
            AllowOthers = allowOthers,
            SortOrder = -style.Priority,
            // Repeated icons use their gap as spacing, otherwise the icon is
            // placed once at the center of the way (renderer convention: a
            // spacing of zero places a single symbol on the path)
            Spacing = style.Repeat ? style.RepeatGap : 0
        };
    }

    /// <summary>
    /// Creates the text line symbol of a pathText layer: a label placed along
    /// the way, repeated when requested.
    /// </summary>
    private ISymbol? CreatePathTextSymbol(Tile tile, MFPathTextLayerStyle style, IFeature feature)
    {
        var (allowOverlap, allowOthers) = GetDisplayBehavior(style.Display);
        if (allowOverlap < 0)
        {
            // Display directive "never": the layer is not rendered
            return null;
        }

        var text = GetText(feature, style.Key, style.TextTransform);
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var geometry = GetFeatureLineGeometry(tile, feature);
        if (geometry == null)
        {
            return null;
        }

        var textLineSymbol = new TextLineSymbol(tile, GetFeatureId(feature), geometry, text)
        {
            Name = GetFeatureName(feature),
            StyleName = style.Name,
            Optional = true,
            AllowOverlap = allowOverlap > 0,
            AllowOthers = allowOthers,
            // Labels of a higher priority are placed first (lower sort order)
            SortOrder = -style.Priority,
            // With rotate, the label follows the path direction and is flipped
            // on reversed segments, otherwise it is drawn upright
            RotationAlignment = style.Rotate ? MapAlignment.Map : MapAlignment.Viewport,
            KeepUpright = true,
            Anchor = new NtsPoint(0.5, 0.5),
            // Vertical offset of the label relative to the path (perpendicular)
            Offset = new NtsPoint(0, -style.Dy),
            Color = (context) => style.Fill,
            FontNames = GetFontNames(style.FontFamily, style.FontStyle),
            FontSize = (context) => style.FontSize * style.BaseTextSize,
            HaloColor = (context) => style.Stroke,
            HaloWidth = (context) => style.StrokeWidth * style.BaseStrokeWidth,
            // The Mapsforge lineSymbol stroke-width is a painted stroke width, not an outward
            // distance like the OMT halo-width (renderer compensation, see TextLineSymbol)
            HaloIsOutsideDistance = false
        };

        return new LineSymbol(tile, GetFeatureId(feature), geometry,
            icon: null, textLineSymbol)
        {
            Name = GetFeatureName(feature),
            StyleName = style.Name,
            AllowOthers = allowOthers,
            SortOrder = -style.Priority,
            // Repeated labels use their gap as spacing, otherwise the label is
            // placed once at the center of the way (renderer convention: a
            // spacing of zero places a single symbol on the path)
            Spacing = style.Repeat ? style.RepeatGap : 0
        };
    }

    /// <summary>
    /// Wraps a caption inline point symbol in the PointSymbol envelope the
    /// placement and draw paths expect (no icon, only text).
    /// </summary>
    private static PointSymbol PointSymbolOf(TextPointSymbol text)
    {
        return new PointSymbol(text.Tile, text.Id, text.Point, icon: null, text)
        {
            Name = text.Name,
            StyleName = text.StyleName,
            AllowOthers = text.AllowOthers,
            SortOrder = text.SortOrder
        };
    }

    /// <summary>
    /// Wraps an icon point symbol in the PointSymbol envelope the placement
    /// and draw paths expect (no text, only icon).
    /// </summary>
    private static PointSymbol PointSymbolOf(IconPointSymbol icon)
    {
        return new PointSymbol(icon.Tile, icon.Id, icon.Point, icon, text: null)
        {
            Name = icon.Name,
            StyleName = icon.StyleName,
            AllowOthers = icon.AllowOthers,
            SortOrder = icon.SortOrder
        };
    }

    /// <summary>
    /// Maps the display directive to the overlap behavior of the symbol:
    /// "always" allows overlap in both directions, "order" overlaps other
    /// symbols but is removed when overlapping an "always" symbol, "ifspace"
    /// only takes space that is free. Returns (-1, false) for "never".
    /// </summary>
    private static (int AllowOverlap, bool AllowOthers) GetDisplayBehavior(MapsforgeDisplay display)
    {
        return display switch
        {
            MapsforgeDisplay.Always => (1, true),
            MapsforgeDisplay.Order => (1, false),
            MapsforgeDisplay.IfSpace => (0, false),
            MapsforgeDisplay.Never => (-1, false),
            _ => (0, false)
        };
    }

    /// <summary>
    /// Anchor of an icon at the feature point for the given position
    /// (attribute position of symbol elements). The anchor is the relative
    /// position of the icon that sits on the feature point, e.g. "below"
    /// places the top edge of the icon at the point.
    /// </summary>
    private static (NtsPoint Anchor, NtsPoint Offset) GetPointAnchor(MapsforgePosition position)
    {
        return position switch
        {
            MapsforgePosition.Below => (new NtsPoint(0.5, 0), new NtsPoint(0, 0)),
            MapsforgePosition.BelowLeft => (new NtsPoint(1, 0), new NtsPoint(0, 0)),
            MapsforgePosition.BelowRight => (new NtsPoint(0, 0), new NtsPoint(0, 0)),
            MapsforgePosition.Above => (new NtsPoint(0.5, 1), new NtsPoint(0, 0)),
            MapsforgePosition.AboveLeft => (new NtsPoint(1, 1), new NtsPoint(0, 0)),
            MapsforgePosition.AboveRight => (new NtsPoint(0, 1), new NtsPoint(0, 0)),
            MapsforgePosition.Left => (new NtsPoint(1, 0.5), new NtsPoint(0, 0)),
            MapsforgePosition.Right => (new NtsPoint(0, 0.5), new NtsPoint(0, 0)),
            // Auto and Center: icon centered on the feature point
            _ => (new NtsPoint(0.5, 0.5), new NtsPoint(0, 0))
        };
    }

    /// <summary>
    /// Anchor and offset of a caption for its position (attribute position):
    /// relative to the referenced icon when the caption refers to one (the
    /// caption is placed next to the icon), centered on the feature point
    /// otherwise. The dy attribute shifts the caption down by its value.
    /// </summary>
    private (NtsPoint Anchor, NtsPoint Offset) GetCaptionAnchor(MFCaptionLayerStyle style)
    {
        var iconSize = GetReferencedIconSize(style);

        // Captions without icon and centered captions sit on the point; the
        // anchor is the relative position of the text block that sits there,
        // push it away from the icon by half of the icon size otherwise
        var (anchor, offset) = iconSize switch
        {
            _ when style.Position == MapsforgePosition.Below
                => (new NtsPoint(0.5, 0), new NtsPoint(0, iconSize.Value.Height / 2f)),
            _ when style.Position == MapsforgePosition.BelowLeft
                => (new NtsPoint(1, 0), new NtsPoint(-iconSize.Value.Width / 2f, iconSize.Value.Height / 2f)),
            _ when style.Position == MapsforgePosition.BelowRight
                => (new NtsPoint(0, 0), new NtsPoint(iconSize.Value.Width / 2f, iconSize.Value.Height / 2f)),
            _ when style.Position == MapsforgePosition.Above
                => (new NtsPoint(0.5, 1), new NtsPoint(0, -iconSize.Value.Height / 2f)),
            _ when style.Position == MapsforgePosition.AboveLeft
                => (new NtsPoint(1, 1), new NtsPoint(-iconSize.Value.Width / 2f, -iconSize.Value.Height / 2f)),
            _ when style.Position == MapsforgePosition.AboveRight
                => (new NtsPoint(0, 1), new NtsPoint(iconSize.Value.Width / 2f, -iconSize.Value.Height / 2f)),
            _ when style.Position == MapsforgePosition.Left
                => (new NtsPoint(1, 0.5), new NtsPoint(-iconSize.Value.Width / 2f, 0)),
            _ when style.Position == MapsforgePosition.Right
                => (new NtsPoint(0, 0.5), new NtsPoint(iconSize.Value.Width / 2f, 0)),
            // Auto and Center: caption centered on the point / the icon
            _ => (new NtsPoint(0.5, 0.5), new NtsPoint(0, 0))
        };

        if (style.Dy != 0)
        {
            offset = new NtsPoint(offset.X, offset.Y + style.Dy);
        }

        return (anchor, offset);
    }

    /// <summary>
    /// Size in px of the icon a caption is positioned relative to, or null
    /// when the caption does not refer to an icon or it cannot be resolved.
    /// </summary>
    private (float Width, float Height)? GetReferencedIconSize(MFCaptionLayerStyle style)
    {
        var symbolStyle = style.PositionSymbol;

        if (symbolStyle == null)
        {
            return null;
        }

        var sprite = _spriteFactory.GetSprite(symbolStyle.Src,
            symbolStyle.SymbolWidth, symbolStyle.SymbolHeight, symbolStyle.SymbolPercent);

        if (sprite == null)
        {
            return null;
        }

        // Sprite sizes are in style px, relative to the pixel ratio of the
        // sprite (matching what the renderer draws at scale one)
        return (sprite.Width / sprite.PixelRatio, sprite.Height / sprite.PixelRatio);
    }

    /// <summary>
    /// Rotation in degrees an icon is rotated by for the given orientation
    /// (attribute symbol-orientation): mapsforge icons point to the right by
    /// default, so "up" rotates 90° counterclockwise and "down" clockwise.
    /// </summary>
    private static float GetOrientationRotation(string orientation)
    {
        return orientation?.ToLowerInvariant() switch
        {
            "left" => 180f,
            "up" => -90f,
            "down" => 90f,
            // "right" and unknown orientations use the icon as drawn
            _ => 0f
        };
    }

    /// <summary>
    /// Gets the text of an attribute key of the feature, with the text
    /// transformation applied.
    /// </summary>
    private static string GetText(IFeature feature, string key, MapsforgeTextTransform transform)
    {
        if (feature.Attributes?.Exists(key) != true)
        {
            return string.Empty;
        }

        var text = feature.Attributes[key]?.ToString() ?? string.Empty;

        return transform switch
        {
            MapsforgeTextTransform.Uppercase => text.ToUpperInvariant(),
            MapsforgeTextTransform.Lowercase => text.ToLowerInvariant(),
            MapsforgeTextTransform.Capitalize => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant()),
            _ => text
        };
    }

    /// <summary>
    /// Gets the font name list for a font family and style, following the
    /// "Family Style" naming convention of the font cache (e.g. "Open Sans
    /// Bold"). Multiple families are given as fallbacks; a missing family
    /// falls back to the system default font.
    /// </summary>
    private static string[] GetFontNames(MapsforgeFontFamily family, MapsforgeFontStyle style)
    {
        var suffix = style switch
        {
            MapsforgeFontStyle.Bold => "Bold",
            MapsforgeFontStyle.Italic => "Italic",
            MapsforgeFontStyle.BoldItalic => "Bold Italic",
            _ => "Regular"
        };

        var families = family switch
        {
            MapsforgeFontFamily.Monospace => new[] { "Roboto Mono", "Courier New", "DejaVu Sans Mono" },
            MapsforgeFontFamily.SansSerif => new[] { "Open Sans", "Roboto", "Arial" },
            MapsforgeFontFamily.Serif => new[] { "Noto Serif", "DejaVu Serif", "Times New Roman" },
            // Default font of the platform
            _ => new[] { "Open Sans", "Arial Unicode MS", "Roboto" }
        };

        return families.Select(f => $"{f} {suffix}").ToArray();
    }

    /// <summary>
    /// The point to place a point symbol at: the feature point itself, or an
    /// interior point for non point geometries. Null for empty geometries and
    /// positions outside the valid coordinate range. The tile coordinates of
    /// the mapsforge vector data (0..512 px) are converted into the world
    /// coordinates of the tile, the space the symbol placement and the draw
    /// pass work with.
    /// </summary>
    private static NtsPoint? GetFeaturePoint(Tile tile, IFeature feature)
    {
        // An empty geometry has no centroid or interior point: NetTopologySuite
        // returns a POINT EMPTY with NaN coordinates, which would break the
        // placement of the symbol
        if (feature.Geometry.IsEmpty)
        {
            return null;
        }

        var geometryPoint = feature.Geometry.Centroid;

        if (feature.Geometry.GeometryType != "Point" || feature.Geometry.Coordinates.Length != 1)
        {
            // Symbol should be a point, but geometry isn't a point, so use an
            // interior point instead
            geometryPoint = feature.Geometry.InteriorPoint;
        }

        var (pointX, pointY) = ConvertFrom(tile, geometryPoint.X, geometryPoint.Y);
        var point = new NtsPoint(pointX, pointY);

        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
        {
            // A non-finite symbol position must not enter the placement: the
            // placement engine would fail on it and the tile would stay
            // incomplete
            return null;
        }

        return point;
    }

    /// <summary>
    /// Gets the line geometry a line symbol (icon along the line or label on
    /// the path) is placed along: the feature geometry itself, converted from
    /// the tile coordinates of the mapsforge vector data (0..512 px) into the
    /// world coordinates of the tile, the space the symbol placement and the
    /// draw pass work with (the same conversion the OpenMapTiles symbol
    /// factory applies to its point symbols). Null for geometries without at
    /// least two points and for non finite coordinates (e.g. clipped away
    /// parts at high overzoom).
    /// </summary>
    private static NetTopologySuite.Geometries.Geometry? GetFeatureLineGeometry(Tile tile, IFeature feature)
    {
        if (feature.Geometry.IsEmpty || feature.Geometry.Coordinates.Length < 2)
        {
            return null;
        }

        foreach (var coordinate in feature.Geometry.Coordinates)
        {
            if (!double.IsFinite(coordinate.X) || !double.IsFinite(coordinate.Y))
            {
                // Features with coordinates outside the tile (e.g. clipped away paths at a
                // high overzoom level) would produce artifacts in the line symbol placement
                return null;
            }
        }

        var worldCoordinates = new NetTopologySuite.Geometries.Coordinate[feature.Geometry.Coordinates.Length];
        for (var i = 0; i < feature.Geometry.Coordinates.Length; i++)
        {
            var (worldX, worldY) = ConvertFrom(tile, feature.Geometry.Coordinates[i].X, feature.Geometry.Coordinates[i].Y);
            worldCoordinates[i] = new NetTopologySuite.Geometries.Coordinate(worldX, worldY);
        }

        return new NetTopologySuite.Geometries.GeometryFactory()
            .CreateLineString(worldCoordinates);
    }

    /// <summary>
    /// Converts tile coordinates (0..512 px of the tile top left corner) into
    /// the world (spherical mercator) coordinates of the tile; the same
    /// conversion the OpenMapTiles symbol factory applies to its symbols.
    /// </summary>
    private static (double X, double Y) ConvertFrom(Tile tile, double pointX, double pointY)
    {
        var worldPointX = tile.Left + (tile.Right - tile.Left) * pointX / 512.0;
        var worldPointY = tile.Top - (tile.Top - tile.Bottom) * pointY / 512.0;

        return (worldPointX, worldPointY);
    }

    /// <summary>
    /// Gets the unique identifier of the feature, or zero when it has none.
    /// </summary>
    private static ulong GetFeatureId(IFeature feature)
    {
        return feature.Attributes?.Exists("id") == true
            && ulong.TryParse(feature.Attributes["id"]?.ToString(), out var id) ? id : 0;
    }

    /// <summary>
    /// Gets the name attribute of the feature, or an empty string.
    /// </summary>
    private static string GetFeatureName(IFeature feature)
    {
        return feature.Attributes?.Exists("name") == true
            ? feature.Attributes["name"]?.ToString() ?? string.Empty
            : string.Empty;
    }
}
