using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Exceptions;
using VecTiles.Common.Extensions;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles.Extensions;
using VecTiles.Styles.OpenMapTiles.Enums;

namespace VecTiles.Styles.OpenMapTiles;

public class OMTTextBuilder
{
    private OMTLayerStyle _style;
    private EvaluationContext _context;
    private string[] _textFont;
    private float _fontSize;
    private float _lineHeight;
    private float _letterSpacing;
    private string _textMask;
    private bool _allowOverlap;
    private bool _optional;
    private bool _allowOthers;
    private float _scale;
    private float _rotation;
    private Point _anchor;
    private Point _offset;
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

    public OMTTextBuilder(OMTLayerStyle style, EvaluationContext context)
    {
        _style = style;
        _context = context;

        if (style.Layout.TextFont == null || style.Layout.TextFont.Length == 0)
        {
            throw new FontNotFoundException($"Font missing for style '{style.Name}'");
        }

        _textFont = style.Layout.TextFont;

        _fontSize = style.Layout.TextSize.Evaluate(_context);
        _lineHeight = style.Layout.TextLineHeight.Evaluate(context);
        _letterSpacing = style.Layout.TextLetterSpacing.Evaluate(context);

        _textMask = style.Layout.TextField;

        _optional = style.Layout.TextOptional;
        _allowOverlap = style.Layout.TextAllowOverlap;
        _allowOthers = style.Layout.TextIgnorePlacement;

        _rotation = style.Layout.TextRotate.Evaluate(context);
        _anchor = style.Layout.TextAnchor.ToPoint();
        _offset = style.Layout.TextOffset.Evaluate(context).ToPoint(_fontSize);

        _transform = style.Layout.TextTransform;
        _alignment = CreateAlignment(style.Layout.TextJustify, style.Layout.TextAnchor);

        _color = (ctx) => style.Paint.TextColor.Evaluate(ctx);
        _opacity = (ctx) => style.Paint.TextOpacity.Evaluate(ctx);
        _haloBlur = (ctx) => style.Paint.TextHaloBlur.Evaluate(ctx);
        _haloColor = (ctx) => style.Paint.TextHaloColor.Evaluate(ctx);
        _haloWidth = (ctx) => style.Paint.TextHaloWidth.Evaluate(ctx);
        _translate = style.Paint.TextTranslate.HasOnlyDefault ? null : (ctx) => style.Paint.TextTranslate.Evaluate(ctx).ToPoint();
        _translateAnchor = (ctx) => style.Paint.TextTranslateAnchor.Evaluate(ctx);
        _maxWidth = (ctx, fontsize) => style.Layout.TextMaxWidth.Evaluate(ctx) * fontsize;

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
        _context.Feature = feature;

        var text = _textMask.ReplaceWithTags(_context)
            .ReplaceWithTransforms(_transform);

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var symbol = new TextPointSymbol(tile, point, text)
        {
            Name = (feature?.Attributes?.Exists("name") ?? false) ? (feature?.Attributes?["name"].ToString() ?? string.Empty) : string.Empty,
            Optional = _optional,
            AllowOverlap = _allowOverlap,
            AllowOthers = _allowOthers,
            Rotation = _rotation,
            Anchor = _anchor,
            Offset = _offset,
            Color = _color,
            Alignment = _alignment,
            Direction = TextDirection.Auto,
            Opacity = _opacity,
            HaloBlur = _haloBlur,
            HaloColor = _haloColor,
            HaloWidth = _haloWidth,
            Translate = _translate,
            TranslateAnchor = _translateAnchor,
            MaxWidth = _maxWidth,
            SortOrder = _sortOrder(_context, tile),
            Class = feature.Attributes.Exists("class") ? feature.Attributes["class"]!.ToString() ?? string.Empty : string.Empty,
            Subclass = feature.Attributes.Exists("subclass") ? feature.Attributes["subclass"].ToString() ?? string.Empty : string.Empty,
            Rank = feature.Attributes.Exists("rank") ? int.Parse(feature.Attributes["rank"]!.ToString() ?? string.Empty) : 0,
        };

        return symbol;
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