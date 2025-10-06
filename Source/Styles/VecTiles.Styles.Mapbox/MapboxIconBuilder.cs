using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapbox.Enums;
using VecTiles.Styles.Mapbox.Extensions;

namespace VecTiles.Styles.Mapbox
{
    public class MapboxIconBuilder
    {
        MapboxLayerStyle _style;
        Func<string, ISprite?> _spriteFactory;
        EvaluationContext _context;
        string _spriteMask;
        bool _allowOverlap;
        bool _optional;
        bool _allowOthers;
        float _scale;
        float _rotation;
        MapAlignment _rotationAlignment;
        int _padding;
        Point _anchor;
        Point _offset;
        float _spacing;
        Func<EvaluationContext, SKColorFilter> _colorFilter;
        Func<EvaluationContext, float> _opacity;
        Func<EvaluationContext, Point> _translate;
        Func<EvaluationContext, MapAlignment> _translateAnchor;
        Func<EvaluationContext, Tile, double> _sortOrder;

        public MapboxIconBuilder(MapboxLayerStyle style, Func<string, ISprite?> spriteFactory, EvaluationContext context)
        {
            _style = style;
            _spriteFactory = spriteFactory;
            _context = context;

            _spriteMask = style.Layout.IconImage.Evaluate(context);

            _optional = style.Layout.IconOptional;
            _allowOverlap = style.Layout.IconAllowOverlap;
            _allowOthers = style.Layout.IconIgnorePlacement;

            _scale = style.Layout.IconSize.Evaluate(context);
            _rotation = style.Layout.IconRotate.Evaluate(context);
            _rotationAlignment = style.Layout.IconRotationAlignment;
            _padding = (int)style.Layout.IconPadding.Evaluate(context);
            _anchor = style.Layout.IconAnchor.ToPoint();
            _offset = style.Layout.IconOffset.Evaluate(context).ToPoint(_scale);
            _spacing = style.Layout.SymbolSpacing.Evaluate(context);

            _colorFilter = (context) => CreateColorFilter(
                style.Paint.IconColorBrightnessMin.Evaluate(context),
                style.Paint.IconColorBrightnessMax.Evaluate(context),
                style.Paint.IconColorContrast.Evaluate(context),
                style.Paint.IconColorSaturation.Evaluate(context),
                style.Paint.IconOpacity.Evaluate(context));
            _opacity = (context) => style.Paint.IconOpacity.Evaluate(context);
            _translate = (context) => style.Paint.IconTranslate.Evaluate(context).ToPoint();
            _translateAnchor = (context) => style.Paint.IconTranslateAnchor.Evaluate(context);

            _sortOrder = (context, tile) => _style.Layout.SymbolZOrder switch
            {
                SymbolZOrder.Source => _style.Layout.SymbolSortKey.Evaluate(context),
                SymbolZOrder.ViewportY => _allowOverlap || _allowOthers ? tile.Y * 512.0 + context.Feature.Geometry.Centroid.Y : 0.0,
                SymbolZOrder.Auto => _style.Layout.SymbolSortKey?.Evaluate(context) ?? (_allowOverlap || _allowOthers ? tile.Y * 512.0 + context.Feature.Geometry.Centroid.Y : 0.0),
                _ => 0.0
            };
        }

        public MapboxIconPointSymbol? Build(Tile tile, Point point, IFeature feature)
        {
            _context.Feature = feature;

            if (!TryGetSprite(_style, _spriteFactory, _context, out var sprite))
            {
                return null;
            }

            var symbol = new MapboxIconPointSymbol(tile, point, sprite!)
            {
                Name = (feature?.Attributes?.Exists("name") ?? false) ? (feature?.Attributes?["name"].ToString() ?? string.Empty) : string.Empty,
                Optional = _optional,
                AllowOverlap = _allowOverlap,
                AllowOthers = _allowOthers,
                Scale = _scale,
                Rotation = _rotation,
                Padding = _padding,
                Anchor = _anchor,
                Offset = _offset,
                ColorFilter = _colorFilter,
                Opacity = _opacity,
                Translate = _translate,
                TranslateAnchor = _translateAnchor,
                SortOrder = _sortOrder(_context, tile),
                Class = feature.Attributes.Exists("class") ? feature.Attributes["class"]!.ToString() ?? string.Empty : string.Empty,
                Subclass = feature.Attributes.Exists("subclass") ? feature.Attributes["subclass"].ToString() ?? string.Empty : string.Empty,
                Rank = feature.Attributes.Exists("rank") ? int.Parse(feature.Attributes["rank"]!.ToString() ?? string.Empty) : 0,
            };

            return symbol;
        }

        public MapboxIconLineSymbol? Build(Tile tile, Geometry geometry, IFeature feature)
        {
            _context.Feature = feature;

            if (!TryGetSprite(_style, _spriteFactory, _context, out var sprite))
            {
                return null;
            }

            var symbol = new MapboxIconLineSymbol(tile, geometry, sprite!)
            {
                Name = (feature?.Attributes?.Exists("name") ?? false) ? (feature?.Attributes?["name"].ToString() ?? string.Empty) : string.Empty,
                Optional = _optional,
                AllowOverlap = _allowOverlap,
                AllowOthers = _allowOthers,
                Scale = _scale,
                Rotation = _rotation,
                RotationAlignment = _rotationAlignment == MapAlignment.Auto ? MapAlignment.Map : _rotationAlignment,
                Padding = _padding,
                Anchor = _anchor,
                Offset = _offset,
                Spacing = _spacing,
                ColorFilter = _colorFilter,
                Opacity = _opacity,
                Translate = _translate,
                TranslateAnchor = _translateAnchor,
                SortOrder = _sortOrder(_context, tile),
                Class = feature.Attributes.Exists("class") ? feature.Attributes["class"]!.ToString() ?? string.Empty : string.Empty,
                Subclass = feature.Attributes.Exists("subclass") ? feature.Attributes["subclass"].ToString() ?? string.Empty : string.Empty,
                Rank = feature.Attributes.Exists("rank") ? int.Parse(feature.Attributes["rank"]!.ToString() ?? string.Empty) : 0,
            };

            return symbol;
        }

        private bool TryGetSprite(MapboxLayerStyle style, Func<string, ISprite?> spriteFactory, EvaluationContext context, out ISprite? sprite)
        {
            if (_spriteMask == string.Empty)
            {
                sprite = null;
                return false;
            }

            var spriteName = _spriteMask.ReplaceWithTags(_context);

            sprite = spriteFactory(spriteName);

            if (sprite == null)
            {
                // TODO: What to do?
                System.Diagnostics.Debug.WriteLine($"Couldn't find sprite with name '{spriteName}'");
                //throw new SpriteNotFoundException($"Couldn't find sprite with name '{spriteName}'");
                return false;
            }

            return true;
        }
    
        private static SKColorFilter CreateColorFilter(float minBrightness, float maxBrightness, float contrast, float saturation, float opacity)
        {
            // Brightness-Transformation
            float brightnessRange = maxBrightness - minBrightness;
            float brightnessOffset = minBrightness;

            // Convert: contrast/saturation ∈ [-1..1] → [0..2]
            float mappedContrast = 1f + contrast;
            float mappedSaturation = 1f + saturation;

            // ITU-R BT.601
            float lumR = 0.2126f;
            float lumG = 0.7152f;
            float lumB = 0.0722f;

            float sr = (1f - mappedSaturation) * lumR;
            float sg = (1f - mappedSaturation) * lumG;
            float sb = (1f - mappedSaturation) * lumB;

            // Contrast Offset
            float contrastOffset = 0.5f * (1f - mappedContrast) * 255f;

            // Final Brightness-Offset
            float brightnessShift = 255f * brightnessOffset;

            // Combined Matrix
            float[] matrix = new float[]
            {
            // Red
            (sr + mappedSaturation) * mappedContrast * brightnessRange,
            sg * mappedContrast * brightnessRange,
            sb * mappedContrast * brightnessRange,
            0,
            contrastOffset + brightnessShift,

            // Green
            sr * mappedContrast * brightnessRange,
            (sg + mappedSaturation) * mappedContrast * brightnessRange,
            sb * mappedContrast * brightnessRange,
            0,
            contrastOffset + brightnessShift,

            // Blue
            sr * mappedContrast * brightnessRange,
            sg * mappedContrast * brightnessRange,
            (sb + mappedSaturation) * mappedContrast * brightnessRange,
            0,
            contrastOffset + brightnessShift,

            // Alpha
            0, 0, 0, opacity, 0
            };

            return SKColorFilter.CreateColorMatrix(matrix);
        }
    }
}