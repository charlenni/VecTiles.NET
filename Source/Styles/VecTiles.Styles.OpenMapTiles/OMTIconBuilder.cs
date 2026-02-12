using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Extensions;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles.Enums;
using VecTiles.Styles.OpenMapTiles.Extensions;

namespace VecTiles.Styles.OpenMapTiles
{
    public class OMTIconBuilder
    {
        private readonly OMTLayerStyle _style;
        private readonly Func<string, ISprite?> _spriteFactory;
        private readonly EvaluationContext _context;
        private readonly string _spriteMask;
        private readonly bool _allowOverlap;
        private readonly bool _optional;
        private readonly bool _allowOthers;
        private readonly float _scale;
        private readonly float _rotation;
        private readonly MapAlignment _rotationAlignment;
        private readonly int _padding;
        private readonly bool _keepUpright;
        private readonly Point _anchor;
        private readonly Point _offset;
        private readonly float _spacing;
        private readonly Func<EvaluationContext, float[]> _colorFilter;
        private readonly Func<EvaluationContext, float> _opacity;
        private readonly Func<EvaluationContext, Point> _translate;
        private readonly Func<EvaluationContext, MapAlignment> _translateAnchor;
        private readonly Func<EvaluationContext, Tile, double> _sortOrder;

        public OMTIconBuilder(OMTLayerStyle style, Func<string, ISprite?> spriteFactory, EvaluationContext context)
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
            _keepUpright = style.Layout.IconKeepUpright;
            _anchor = style.Layout.IconAnchor.ToPoint();
            _offset = style.Layout.IconOffset.Evaluate(context).ToPoint(_scale);

            _colorFilter = (ctx) => CreateColorFilter(
                style.Paint.IconColorBrightnessMin.Evaluate(ctx),
                style.Paint.IconColorBrightnessMax.Evaluate(ctx),
                style.Paint.IconColorContrast.Evaluate(ctx),
                style.Paint.IconColorSaturation.Evaluate(ctx),
                style.Paint.IconOpacity.Evaluate(ctx));
            _opacity = (ctx) => style.Paint.IconOpacity.Evaluate(ctx);
            _translate = (ctx) => style.Paint.IconTranslate.Evaluate(ctx).ToPoint();
            _translateAnchor = (ctx) => style.Paint.IconTranslateAnchor.Evaluate(ctx);

            _sortOrder = (ctx, tile) => _style.Layout.SymbolZOrder switch
            {
                SymbolZOrder.Source => _style.Layout.SymbolSortKey.Evaluate(ctx),
                SymbolZOrder.ViewportY => _allowOverlap || _allowOthers ? tile.Y * 512.0 + ctx.Feature?.Geometry.Centroid.Y ?? 0.0 : 0.0,
                SymbolZOrder.Auto => _style.Layout.SymbolSortKey?.Evaluate(ctx) ?? (_allowOverlap || _allowOthers ? tile.Y * 512.0 + ctx.Feature?.Geometry.Centroid.Y ?? 0.0 : 0.0),
                _ => 0.0
            };
        }

        public IconPointSymbol? Build(Tile tile, Point point, IFeature feature)
        {
            _context.Feature = feature;

            if (!TryGetSprite(_style, _spriteFactory, _context, out var sprite))
            {
                return null;
            }

            var id = (ulong)feature.Attributes["id"];

            var symbol = new IconPointSymbol(tile, id, point, sprite!)
            {
                Name = (feature?.Attributes?.Exists("name") ?? false) ? (feature?.Attributes?["name"].ToString() ?? string.Empty) : string.Empty,
                Optional = _optional,
                AllowOverlap = _allowOverlap,
                AllowOthers = _allowOthers,
                Scale = _scale,
                Rotation = _rotation,
                Padding = _padding,
                KeepUpright = _keepUpright,
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

        public IconLineSymbol? Build(Tile tile, Geometry geometry, IFeature feature)
        {
            _context.Feature = feature;

            if (!TryGetSprite(_style, _spriteFactory, _context, out var sprite))
            {
                return null;
            }

            var id = (ulong)feature.Attributes["id"];

            var symbol = new IconLineSymbol(tile, id, geometry, sprite!)
            {
                Name = (feature?.Attributes?.Exists("name") ?? false) ? (feature?.Attributes?["name"].ToString() ?? string.Empty) : string.Empty,
                Optional = _optional,
                AllowOverlap = _allowOverlap,
                AllowOthers = _allowOthers,
                Scale = _scale,
                Rotation = _rotation,
                RotationAlignment = _rotationAlignment == MapAlignment.Auto ? MapAlignment.Map : _rotationAlignment,
                Padding = _padding,
                KeepUpright = _keepUpright,
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

        private bool TryGetSprite(OMTLayerStyle style, Func<string, ISprite?> spriteFactory, EvaluationContext context, out ISprite? sprite)
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
    
        private static float[] CreateColorFilter(float minBrightness, float maxBrightness, float contrast, float saturation, float opacity)
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
            float[] matrix =
            [
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
            ];

            return matrix;
        }
    }
}