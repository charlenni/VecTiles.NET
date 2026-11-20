using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using ProtoBuf.Serializers;
using VecTiles.Common.Enums;
using VecTiles.Common.Exceptions;
using VecTiles.Common.Extensions;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles.Enums;
using VecTiles.Styles.OpenMapTiles.Expressions;
using VecTiles.Styles.OpenMapTiles.Extensions;

namespace VecTiles.Styles.OpenMapTiles;

public class OMTTextBuilder
{
    private OMTLayerStyle _style;
    private EvaluationContext _context;
    private string[] _fontNames;
    private Func<EvaluationContext, float> _fontSize;
    private float _lineHeight;
    private float _letterSpacing;
    private string _textMask;
    private bool _allowOverlap;
    private bool _optional;
    private bool _allowOthers;
    private float _scale;
    private float _rotation;
    private readonly MapAlignment _rotationAlignment;
    private bool _keepUpright;
    private readonly int _padding;
    private Point _anchor;
    private Point _offset;
    private readonly float _spacing;
    private TextTransform _transform;
    private TextJustify _alignment;
    private Func<EvaluationContext, Color> _color;
    private Func<EvaluationContext, float> _opacity;
    private Func<EvaluationContext, float> _haloBlur;
    private Func<EvaluationContext, Color> _haloColor;
    private Func<EvaluationContext, float> _haloWidth;
    private Func<EvaluationContext, Point>? _translate;
    private Func<EvaluationContext, MapAlignment> _translateAnchor;
    private Func<EvaluationContext, Tile, double> _sortOrder;
    private Func<EvaluationContext, float, float> _maxWidth;
    private Func<EvaluationContext, float> _maxAngle;
    private readonly StoppedString _textField;
    private readonly StoppedFloat _textLineHeight;
    private readonly StoppedFloat _textLetterSpacing;
    private readonly StoppedFloat _textRotate;
    private readonly StoppedFloatArray _textOffset;
    private readonly StoppedFloat _textPadding;
    private readonly StoppedEnum<TextTransform> _textTransform;
    private readonly StoppedBoolean _textAllowOverlap;
    private readonly StoppedBoolean _textIgnorePlacement;

    /// <summary>
    /// Fonts used for styles that do not specify a text-font.
    /// </summary>
    public static readonly string[] DefaultFontNames = ["Open Sans Regular", "Arial Unicode MS Regular"];

    public OMTTextBuilder(OMTLayerStyle style, EvaluationContext context)
    {
        _style = style;
        _context = context;

        if (style.Layout.TextFont == null || style.Layout.TextFont.Length == 0)
        {
            _fontNames = DefaultFontNames;
        }
        else
        {
            _fontNames = style.Layout.TextFont;
        }
        _fontSize = (ctx) => style.Layout.TextSize.Evaluate(ctx);

        _textField = style.Layout.TextField;
        _textLineHeight = style.Layout.TextLineHeight;
        _textLetterSpacing = style.Layout.TextLetterSpacing;
        _textRotate = style.Layout.TextRotate;
        _textOffset = style.Layout.TextOffset;
        _textPadding = style.Layout.TextPadding;
        _textTransform = style.Layout.TextTransform;
        _textAllowOverlap = style.Layout.TextAllowOverlap;
        _textIgnorePlacement = style.Layout.TextIgnorePlacement;

        _lineHeight = style.Layout.TextLineHeight.Evaluate(context);
        _letterSpacing = style.Layout.TextLetterSpacing.Evaluate(context);

        _textMask = style.Layout.TextField.Evaluate(context);

        _optional = style.Layout.TextOptional;
        _allowOverlap = style.Layout.TextAllowOverlap.Evaluate(context);
        _allowOthers = style.Layout.TextIgnorePlacement.Evaluate(context);

        _rotation = style.Layout.TextRotate.Evaluate(context);
        _rotationAlignment = style.Layout.TextRotationAlignment;
        _keepUpright = style.Layout.TextKeepUpright;
        _padding = (int)style.Layout.TextPadding.Evaluate(context);
        _anchor = style.Layout.TextAnchor.ToPoint();
        _offset = style.Layout.TextOffset.Evaluate(context).ToPoint(_fontSize.Invoke(context));

        _transform = style.Layout.TextTransform.Evaluate(context);
        _alignment = CreateAlignment(style.Layout.TextJustify, style.Layout.TextAnchor);

        _color = (ctx) => style.Paint.TextColor.Evaluate(ctx);
        _opacity = (ctx) => style.Paint.TextOpacity.Evaluate(ctx);
        _haloBlur = (ctx) => style.Paint.TextHaloBlur.Evaluate(ctx);
        _haloColor = (ctx) => style.Paint.TextHaloColor.Evaluate(ctx);
        _haloWidth = (ctx) => style.Paint.TextHaloWidth.Evaluate(ctx);
        _translate = style.Paint.TextTranslate.HasOnlyDefault ? null : (ctx) => style.Paint.TextTranslate.Evaluate(ctx).ToPoint();
        _translateAnchor = (ctx) => style.Paint.TextTranslateAnchor.Evaluate(ctx);
        _maxWidth = (ctx, fontsize) => style.Layout.TextMaxWidth.Evaluate(ctx) * fontsize;
        _maxAngle = (ctx) => style.Layout.TextMaxAngle.Evaluate(ctx);

        _sortOrder = (ctx, tile) => _style.Layout.SymbolZOrder switch
        {
            SymbolZOrder.Source => (double)(_style.Layout.SymbolSortKey?.Evaluate(ctx) ?? 0.0),
            SymbolZOrder.ViewportY => (double)(_allowOverlap || _allowOthers ? tile.Y * 512.0 + ctx.Feature.Geometry.Centroid.Y : 0.0),
            SymbolZOrder.Auto => (double)(_style.Layout.SymbolSortKey?.Evaluate(ctx) ?? (_allowOverlap || _allowOthers ? tile.Y * 512.0 + ctx.Feature.Geometry.Centroid.Y : 0.0)),
            _ => 0.0
        };
    }

    public TextPointSymbol? Build(Tile tile, Point point, IFeature feature)
    {
        var context = _context.WithFeature(feature);

        var text = GetTextMask(context).ReplaceWithTags(context)
            .ReplaceWithTransforms(GetTransform(context));

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var id = feature.Attributes.Exists("id") ? (ulong)feature.Attributes["id"] : 0;
        
        var symbol = new TextPointSymbol(tile, id, point, text)
        {
            Name = (feature?.Attributes?.Exists("name") ?? false) ? (feature?.Attributes?["name"].ToString() ?? string.Empty) : string.Empty,
            StyleName = _style.Name,
            Optional = _optional,
            AllowOverlap = GetAllowOverlap(context),
            AllowOthers = GetAllowOthers(context),
            Rotation = GetRotation(context),
            RotationAlignment = _rotationAlignment == MapAlignment.Auto ? MapAlignment.Viewport : _rotationAlignment,
            Padding = GetPadding(context),
            Anchor = _anchor,
            Offset = GetOffset(context),
            Color = _color,
            Alignment = _alignment,
            Direction = TextDirection.Auto,
            Opacity = _opacity,
            FontNames = _fontNames,
            FontSize = _fontSize,
            LineHeight = GetLineHeight(context),
            LetterSpacing = GetLetterSpacing(context),
            HaloBlur = _haloBlur,
            HaloColor = _haloColor,
            HaloWidth = _haloWidth,
            Translate = _translate,
            TranslateAnchor = _translateAnchor,
            MaxWidth = _maxWidth,
            SortOrder = _sortOrder(context, tile),
            Class = feature.Attributes.Exists("class") ? feature.Attributes["class"]!.ToString() ?? string.Empty : string.Empty,
            Subclass = feature.Attributes.Exists("subclass") ? feature.Attributes["subclass"].ToString() ?? string.Empty : string.Empty,
            Rank = feature.Attributes.Exists("rank") ? int.Parse(feature.Attributes["rank"]!.ToString() ?? string.Empty) : 0,
        };

        return symbol;
    }

    public TextLineSymbol? Build(Tile tile, Geometry geometry, IFeature feature)
    {
        var context = _context.WithFeature(feature);

        var text = GetTextMask(context).ReplaceWithTags(context)
            .ReplaceWithTransforms(GetTransform(context));

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var id = feature.Attributes.Exists("id") ? (ulong)feature.Attributes["id"] : 0;

        var symbol = new TextLineSymbol(tile, id, geometry, text)
        {
            Name = (feature?.Attributes?.Exists("name") ?? false) ? (feature?.Attributes?["name"].ToString() ?? string.Empty) : string.Empty,
            StyleName = _style.Name,
            Optional = _optional,
            AllowOverlap = GetAllowOverlap(context),
            AllowOthers = GetAllowOthers(context),
            Rotation = GetRotation(context),
            RotationAlignment = _rotationAlignment == MapAlignment.Auto ? MapAlignment.Map : _rotationAlignment,
            KeepUpright = _keepUpright,
            MaxAngle = _maxAngle,
            Padding = GetPadding(context),
            Anchor = _anchor,
            Offset = GetOffset(context),
            Color = _color,
            Alignment = _alignment,
            Direction = TextDirection.Auto,
            Opacity = _opacity,
            FontNames = _fontNames,
            FontSize = _fontSize,
            LineHeight = GetLineHeight(context),
            LetterSpacing = GetLetterSpacing(context),
            HaloBlur = _haloBlur,
            HaloColor = _haloColor,
            HaloWidth = _haloWidth,
            Translate = _translate,
            TranslateAnchor = _translateAnchor,
            MaxWidth = _maxWidth,
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

    private string GetTextMask(EvaluationContext context)
    {
        return _textField.Expression != null ? _textField.Evaluate(context) : _textMask;
    }

    private TextTransform GetTransform(EvaluationContext context)
    {
        return _textTransform.Expression != null ? _textTransform.Evaluate(context) : _transform;
    }

    private float GetLineHeight(EvaluationContext context)
    {
        return _textLineHeight.Expression != null ? _textLineHeight.Evaluate(context) : _lineHeight;
    }

    private float GetLetterSpacing(EvaluationContext context)
    {
        return _textLetterSpacing.Expression != null ? _textLetterSpacing.Evaluate(context) : _letterSpacing;
    }

    private float GetRotation(EvaluationContext context)
    {
        return _textRotate.Expression != null ? _textRotate.Evaluate(context) : _rotation;
    }

    private int GetPadding(EvaluationContext context)
    {
        return _textPadding.Expression != null ? (int)_textPadding.Evaluate(context) : _padding;
    }

    private Point GetOffset(EvaluationContext context)
    {
        return _textOffset.Expression != null ?
            _textOffset.Evaluate(context).ToPoint(_fontSize.Invoke(context)) : _offset;
    }

    private bool GetAllowOverlap(EvaluationContext context)
    {
        return _textAllowOverlap.Expression != null ? _textAllowOverlap.Evaluate(context) : _allowOverlap;
    }

    private bool GetAllowOthers(EvaluationContext context)
    {
        return _textIgnorePlacement.Expression != null ? _textIgnorePlacement.Evaluate(context) : _allowOthers;
    }

    private static TextJustify CreateAlignment(TextJustify justify, Anchor anchor)
    {
        return justify switch
        {
            TextJustify.Left => TextJustify.Left,
            TextJustify.Center => TextJustify.Center,
            TextJustify.Right => TextJustify.Right,
            TextJustify.Auto => anchor switch
            {
                Anchor.TopLeft => TextJustify.Left,
                Anchor.Left => TextJustify.Left,
                Anchor.BottomLeft => TextJustify.Left,
                Anchor.Top or Anchor.Center or Anchor.Bottom => TextJustify.Center,
                Anchor.TopRight => TextJustify.Right,
                Anchor.Right => TextJustify.Right,
                Anchor.BottomRight => TextJustify.Right,
                _ => TextJustify.Center
            },
            _ => TextJustify.Center
        };
    }
}
