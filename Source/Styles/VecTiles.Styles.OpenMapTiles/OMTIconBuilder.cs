using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Extensions;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles.Enums;
using VecTiles.Styles.OpenMapTiles.Expressions;
using VecTiles.Styles.OpenMapTiles.Extensions;

namespace VecTiles.Styles.OpenMapTiles
{
    /// <summary>
    /// Builds icons for symbols of an OpenMapTiles layer, evaluating the icon
    /// layout and paint properties of the layer style.
    /// </summary>
    public class OMTIconBuilder
    {
        private readonly OMTLayerStyle _style;
        private readonly Func<string, ISprite?> _spriteFactory;
        private readonly EvaluationContext _context;
        private readonly StoppedString _iconImage;
        private readonly StoppedFloat _iconSize;
        private readonly StoppedFloat _iconRotate;
        private readonly StoppedFloatArray _iconOffset;
        private readonly StoppedFloat _iconPadding;
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

        /// <summary>
        /// Initializes the icon builder with a layer style and evaluation context,
        /// resolving the icon layout and paint properties.
        /// </summary>
        /// <param name="style">The symbol layer style whose icon properties are used.</param>
        /// <param name="spriteFactory">Factory that resolves a sprite name into a sprite.</param>
        /// <param name="context">The evaluation context the layer was created with.</param>
        public OMTIconBuilder(OMTLayerStyle style, Func<string, ISprite?> spriteFactory, EvaluationContext context)
        {
            _style = style;
            _spriteFactory = spriteFactory;
            _context = context;

            _iconImage = style.Layout.IconImage;
            _iconSize = style.Layout.IconSize;
            _iconRotate = style.Layout.IconRotate;
            _iconOffset = style.Layout.IconOffset;
            _iconPadding = style.Layout.IconPadding;

            _optional = style.Layout.IconOptional;
            _allowOverlap = style.Layout.IconAllowOverlap;
            _allowOthers = style.Layout.IconIgnorePlacement;

            _scale = style.Layout.IconSize.Evaluate(context);
            _rotation = style.Layout.IconRotate.Evaluate(context);
            _rotationAlignment = style.Layout.IconRotationAlignment;
            _padding = (int)style.Layout.IconPadding.Evaluate(context);
            _keepUpright = style.Layout.IconKeepUpright;
            _anchor = style.Layout.IconAnchor.ToPoint();
            _offset = style.Layout.IconOffset.Evaluate(context).ToPoint();

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

        /// <summary>
        /// Builds an icon point symbol for a point feature.
        /// </summary>
        /// <param name="tile">The tile the feature belongs to.</param>
        /// <param name="point">The anchor position of the icon in tile coordinates.</param>
        /// <param name="feature">The feature to build the icon for.</param>
        /// <returns>The icon point symbol, or null if the icon image could not be resolved.</returns>
        public IconPointSymbol? Build(Tile tile, Point point, IFeature feature)
        {
            var context = _context.WithFeature(feature);

            if (!TryGetSprite(_style, _spriteFactory, context, out var sprite))
            {
                return null;
            }

            var id = feature.Attributes.Exists("id") ? (ulong)feature.Attributes["id"] : 0;

            var symbol = new IconPointSymbol(tile, id, point, sprite!)
            {
                Name = (feature?.Attributes?.Exists("name") ?? false) ? (feature?.Attributes?["name"].ToString() ?? string.Empty) : string.Empty,
                StyleName = _style.Name,
                Optional = _optional,
                AllowOverlap = _allowOverlap,
                AllowOthers = _allowOthers,
                Scale = GetScale(context),
                Rotation = GetRotation(context),
                RotationAlignment = _rotationAlignment == MapAlignment.Auto ? MapAlignment.Viewport : _rotationAlignment,
                Padding = GetPadding(context),
                KeepUpright = _keepUpright,
                Anchor = _anchor,
                Offset = GetOffset(context),
                ColorFilter = _colorFilter,
                Opacity = _opacity,
                Translate = _translate,
                TranslateAnchor = _translateAnchor,
                SortOrder = _sortOrder(context, tile),
                Class = feature.Attributes.Exists("class") ? feature.Attributes["class"]!.ToString() ?? string.Empty : string.Empty,
                Subclass = feature.Attributes.Exists("subclass") ? feature.Attributes["subclass"].ToString() ?? string.Empty : string.Empty,
                Rank = feature.Attributes.Exists("rank") ? int.Parse(feature.Attributes["rank"]!.ToString() ?? string.Empty) : 0,
            };

            return symbol;
        }

        /// <summary>
        /// Builds an icon line symbol for a linear or polygonal feature (an icon
        /// placed along the line or outline).
        /// </summary>
        /// <param name="tile">The tile the feature belongs to.</param>
        /// <param name="geometry">The geometry the icon follows.</param>
        /// <param name="feature">The feature to build the icon for.</param>
        /// <returns>The icon line symbol, or null if the icon image could not be resolved.</returns>
        public IconLineSymbol? Build(Tile tile, Geometry geometry, IFeature feature)
        {
            var context = _context.WithFeature(feature);

            if (!TryGetSprite(_style, _spriteFactory, context, out var sprite))
            {
                return null;
            }

            var id = (ulong)feature.Attributes["id"];

            var symbol = new IconLineSymbol(tile, id, geometry, sprite!)
            {
                Name = (feature?.Attributes?.Exists("name") ?? false) ? (feature?.Attributes?["name"].ToString() ?? string.Empty) : string.Empty,
                StyleName = _style.Name,
                Optional = _optional,
                AllowOverlap = _allowOverlap,
                AllowOthers = _allowOthers,
                Scale = GetScale(context),
                Rotation = GetRotation(context),
                RotationAlignment = _rotationAlignment == MapAlignment.Auto ? MapAlignment.Map : _rotationAlignment,
                Padding = GetPadding(context),
                KeepUpright = _keepUpright,
                Anchor = _anchor,
                Offset = GetOffset(context),
                ColorFilter = _colorFilter,
                Opacity = _opacity,
                Translate = _translate,
                TranslateAnchor = _translateAnchor,
                SortOrder = _sortOrder(context, tile),
                Class = feature.Attributes.Exists("class") ? feature.Attributes["class"]!.ToString() ?? string.Empty : string.Empty,
                Subclass = feature.Attributes.Exists("subclass") ? feature.Attributes["subclass"].ToString() ?? string.Empty : string.Empty,
                Rank = feature.Attributes.Exists("rank") ? int.Parse(feature.Attributes["rank"]!.ToString() ?? string.Empty) : 0,
            };

            return symbol;
        }

        // The layout properties below can be data-driven (expressions per
        // Mapbox Style Spec), so they are evaluated per feature if needed and
        // only fall back to the value frozen in the constructor otherwise.

        private float GetScale(EvaluationContext context)
        {
            return _iconSize.Expression != null ? _iconSize.Evaluate(context) : _scale;
        }

        private float GetRotation(EvaluationContext context)
        {
            return _iconRotate.Expression != null ? _iconRotate.Evaluate(context) : _rotation;
        }

        private int GetPadding(EvaluationContext context)
        {
            return _iconPadding.Expression != null ? (int)_iconPadding.Evaluate(context) : _padding;
        }

        private Point GetOffset(EvaluationContext context)
        {
            return _iconOffset.Expression != null ? _iconOffset.Evaluate(context).ToPoint() : _offset;
        }

        private bool TryGetSprite(OMTLayerStyle style, Func<string, ISprite?> spriteFactory, EvaluationContext context, out ISprite? sprite)
        {
            // The icon image can depend on the feature (expressions, stopped values, field
            // replacement), so it has to be evaluated per feature
            var spriteMask = _iconImage.Evaluate(context);

            if (spriteMask == string.Empty)
            {
                sprite = null;
                return false;
            }

            var spriteName = spriteMask.ReplaceWithTags(context);

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
