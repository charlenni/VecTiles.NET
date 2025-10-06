using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapbox.Enums;
using VecTiles.Styles.Mapbox.Extensions;

namespace VecTiles.Styles.Mapbox;

public class MapboxTextBuilder
{
    MapboxLayerStyle _style;
    EvaluationContext _context;
    Topten.RichTextKit.Style _textStyle;
    string _textMask;
    bool _allowOverlap;
    bool _optional;
    bool _allowOthers;
    float _scale;
    float _rotation;
    SKPoint _anchor;
    SKPoint _offset;
    TextTransform _transform;
    TextJustify _alignment;
    Func<EvaluationContext, Color> _color;
    Func<EvaluationContext, float> _opacity;
    Func<EvaluationContext, float> _haloBlur;
    Func<EvaluationContext, Color> _haloColor;
    Func<EvaluationContext, float> _haloWidth;
    Func<EvaluationContext, SKPoint>? _translate;
    Func<EvaluationContext, MapAlignment> _translateAnchor;
    Func<EvaluationContext, Tile, double> _sortOrder;
    Func<EvaluationContext, float, float> _maxWidth;

    public MapboxTextBuilder(MapboxLayerStyle style, Func<string[], Topten.RichTextKit.Style> fontFactory, EvaluationContext context)
    {
        _style = style;
        _context = context;

        if (style.Layout.TextFont == null || style.Layout.TextFont.Length == 0)
        {
            throw new FontNotFoundException($"Font missing for style '{style.Name}'");
        }

        _textStyle = fontFactory(style.Layout.TextFont);

        if (_textStyle == null)
        {
            throw new FontNotFoundException(style.Layout.TextFont.Length == 1 ? $"Font '{style.Layout.TextFont[0]}' not found" : $"Fonts '{string.Join(", ", style.Layout.TextFont)}' not found");
        }

        var fontSize = style.Layout.TextSize.Evaluate(_context);

        _textStyle.FontSize = fontSize;
        _textStyle.LineHeight = style.Layout.TextLineHeight.Evaluate(context);
        _textStyle.LetterSpacing = style.Layout.TextLetterSpacing.Evaluate(context);

        _textMask = style.Layout.TextField;

        _optional = style.Layout.TextOptional;
        _allowOverlap = style.Layout.TextAllowOverlap;
        _allowOthers = style.Layout.TextIgnorePlacement;

        _rotation = style.Layout.TextRotate.Evaluate(context);
        _anchor = style.Layout.TextAnchor.ToPoint();
        _offset = style.Layout.TextOffset.Evaluate(context).ToPoint(fontSize);

        _transform = style.Layout.TextTransform;
        _alignment = CreateAlignment(style.Layout.TextJustify, style.Layout.TextAnchor);

        _color = (context) => style.Paint.TextColor.Evaluate(context);
        _opacity = (context) => style.Paint.TextOpacity.Evaluate(context);
        _haloBlur = (context) => style.Paint.TextHaloBlur.Evaluate(context);
        _haloColor = (context) => style.Paint.TextHaloColor.Evaluate(context);
        _haloWidth = (context) => style.Paint.TextHaloWidth.Evaluate(context);
        _translate = style.Paint.TextTranslate.HasOnlyDefault ? null : (context) => style.Paint.TextTranslate.Evaluate(context).ToPoint();
        _translateAnchor = (context) => style.Paint.TextTranslateAnchor.Evaluate(context);
        _maxWidth = (context, fontsize) => style.Layout.TextMaxWidth.Evaluate(_context) * fontsize;

        _sortOrder = (context, tile) => _style.Layout.SymbolZOrder switch
        {
            SymbolZOrder.Source => (double)(_style.Layout.SymbolSortKey?.Evaluate(context) ?? 0.0),
            SymbolZOrder.ViewportY => (double)(_allowOverlap || _allowOthers ? tile.Y * 512.0 + context.Feature.Geometry.Centroid.Y : 0.0),
            SymbolZOrder.Auto => (double)(_style.Layout.SymbolSortKey?.Evaluate(context) ?? (_allowOverlap || _allowOthers ? tile.Y * 512.0 + context.Feature.Geometry.Centroid.Y : 0.0)),
            _ => 0.0
        };
    }

    public MapboxTextPointSymbol? Build(Tile tile, Point point, IFeature feature)
    {
        _context.Feature = feature;

        var textBlock = new TextBlock();

        textBlock.MaxWidth = _maxWidth(_context, _textStyle.FontSize) * 3/4;
        textBlock.BaseDirection = TextDirection.Auto;
        textBlock.Alignment = _alignment;

        var text = _textMask.ReplaceWithTags(_context)
            .ReplaceWithTransforms(_transform);

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        textBlock.AddText(text, _textStyle);

        if (textBlock == null)
        {
            return null;
        }

        var symbol = new MapboxTextPointSymbol(tile, point, textBlock)
        {
            Name = (feature?.Attributes?.Exists("name") ?? false) ? (feature?.Attributes?["name"].ToString() ?? string.Empty) : string.Empty,
            Optional = _optional,
            AllowOverlap = _allowOverlap,
            AllowOthers = _allowOthers,
            Rotation = _rotation,
            Anchor = _anchor,
            Offset = _offset,
            Color = _color,
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

    private TextJustify CreateAlignment(TextJustify justify, Anchor anchor)
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
                Anchor.Top => TextJustify.Center,
                Anchor.Center => TextJustify.Center,
                Anchor.Bottom => TextJustify.Center,
                Anchor.TopRight => TextJustify.Right,
                Anchor.Right => TextJustify.Right,
                Anchor.BottomRight => TextJustify.Right,
                _ => TextJustify.Center
            },
            _ => TextJustify.Center
        };
    }
}