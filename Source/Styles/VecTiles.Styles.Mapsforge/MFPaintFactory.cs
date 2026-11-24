using VecTiles.Common.Interfaces;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Creates the paints for the layer styles of a Mapsforge render theme,
/// resolving icon and pattern sources through the given sprite factory.
/// </summary>
public class MFPaintFactory : IPaintFactory
{
    private readonly MFSpriteFactory _spriteFactory;

    /// <summary>
    /// Initializes the factory with the sprite factory of its render theme.
    /// </summary>
    /// <param name="spriteFactory">Factory that resolves icon and pattern sources into sprites.</param>
    public MFPaintFactory(MFSpriteFactory spriteFactory)
    {
        _spriteFactory = spriteFactory;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown for an unknown layer style type.</exception>
    public IPaint? CreatePaint(ILayerStyle style)
    {
        return style.StyleType switch
        {
            LayerStyleType.Background => MFBackgroundPaint.CreatePaint(style),
            LayerStyleType.Fill => MFFillPaint.CreatePaint(style, _spriteFactory),
            LayerStyleType.Line => MFLinePaint.CreatePaint(style, _spriteFactory),
            LayerStyleType.Circle => MFCirclePaint.CreatePaint(style),
            // Symbol layers produce symbols, no paint
            LayerStyleType.Symbol => null,
            _ => throw new ArgumentException($"Layer style with type '{style.StyleType}' is not supported by Mapsforge render themes")
        };
    }
}

/// <summary>
/// Creates the paint for the background layer of a Mapsforge render theme.
/// </summary>
public static class MFBackgroundPaint
{
    /// <summary>
    /// Creates a fill paint from the background attributes of the theme.
    /// </summary>
    /// <param name="style">The layer style; must be an <see cref="MFBackgroundLayerStyle"/>.</param>
    /// <returns>The paint for the background layer.</returns>
    public static Paint CreatePaint(ILayerStyle style)
    {
        if (style is not MFBackgroundLayerStyle backgroundStyle)
        {
            throw new ArgumentException($"Style {style.GetType()} isn't a MFBackgroundLayerStyle");
        }

        var brush = new Paint(style.Name);

        brush.SetFixStyle(PaintStyle.Fill);
        brush.SetFixColor(backgroundStyle.MapBackground);

        return brush;
    }
}

/// <summary>
/// Creates the paint for the fill layers of a Mapsforge render theme.
/// </summary>
public static class MFFillPaint
{
    /// <summary>
    /// Creates a fill paint from the area attributes (fill, stroke,
    /// stroke-width, src) of the layer.
    /// </summary>
    /// <param name="style">The layer style; must be an <see cref="MFFillLayerStyle"/>.</param>
    /// <param name="spriteFactory">Factory that resolves the pattern source into a sprite.</param>
    /// <returns>The paint for the fill layer.</returns>
    public static Paint CreatePaint(ILayerStyle style, MFSpriteFactory spriteFactory)
    {
        if (style is not MFFillLayerStyle fillStyle)
        {
            throw new ArgumentException($"Style {style.GetType()} isn't a MFFillLayerStyle");
        }

        var brush = new Paint(style.Name);

        brush.SetFixStyle(PaintStyle.Fill);
        brush.SetFixStrokeJoin(StrokeJoin.Round);

        if (fillStyle.PatternSrc != null)
        {
            // A pattern replaces the color of the fill
            brush.SetFixColor(Colors.White);
            brush.SetFixPattern(spriteFactory.GetSprite(fillStyle.PatternSrc));
        }
        else
        {
            brush.SetFixColor(fillStyle.Fill);
        }

        brush.SetFixOutlineColor(fillStyle.Stroke);
        brush.SetFixOutlineOpacity(fillStyle.Stroke.A / 255f);
        // Stroke width grows with the zoom like in the original renderer
        // (1.5 per tile level above 12, see MFScaling for the px mapping of
        // the 512 px tiles)
        brush.SetVariableStrokeWidth((context) => fillStyle.StrokeWidth
            * MFFillPaint.GetBaseStrokeWidth(fillStyle)
            * MFScaling.GetStrokeScale(context));

        return brush;
    }

    /// <summary>
    /// Gets the base stroke width factor of the theme of the layer.
    /// </summary>
    internal static float GetBaseStrokeWidth(ILayerStyle style)
    {
        return BaseStrokeWidthAccessor.TryGet(style) ?? 1f;
    }

    /// <summary>
    /// Gets the base text size factor of the theme of the layer.
    /// </summary>
    internal static float GetBaseTextSize(ILayerStyle style)
    {
        return BaseTextSizeAccessor.TryGet(style) ?? 1f;
    }
}

/// <summary>
/// Creates the paint for the line layers of a Mapsforge render theme.
/// </summary>
public static class MFLinePaint
{
    /// <summary>
    /// Creates a stroke paint from the line attributes (stroke, stroke-width,
    /// stroke-dasharray, stroke-linecap, stroke-linejoin, src) of the layer.
    /// </summary>
    /// <param name="style">The layer style; must be an <see cref="MFLineLayerStyle"/>.</param>
    /// <param name="spriteFactory">Factory that resolves the pattern source into a sprite.</param>
    /// <returns>The paint for the line layer.</returns>
    public static Paint CreatePaint(ILayerStyle style, MFSpriteFactory spriteFactory)
    {
        if (style is not MFLineLayerStyle lineStyle)
        {
            throw new ArgumentException($"Style {style.GetType()} isn't a MFLineLayerStyle");
        }

        var brush = new Paint(style.Name);

        brush.SetFixStyle(PaintStyle.Stroke);

        if (lineStyle.PatternSrc != null)
        {
            // A pattern replaces the color of the line
            brush.SetFixColor(Colors.White);
            brush.SetFixPattern(spriteFactory.GetSprite(lineStyle.PatternSrc));
        }
        else
        {
            brush.SetFixColor(lineStyle.Stroke);
        }

        // Stroke width grows with the zoom like in the original renderer
        // (1.5 per tile level above 12, see MFScaling for the px mapping of
        // the 512 px tiles)
        brush.SetVariableStrokeWidth((context) => lineStyle.StrokeWidth
            * MFFillPaint.GetBaseStrokeWidth(lineStyle)
            * MFScaling.GetStrokeScale(context));
        brush.SetFixStrokeCap(lineStyle.Cap);
        brush.SetFixStrokeJoin(lineStyle.Join);

        if (lineStyle.StrokeDasharray is { Length: > 0 } dashArray)
        {
            brush.SetFixDashArray(dashArray.Select(d => d * MFFillPaint.GetBaseStrokeWidth(lineStyle)).ToArray());
        }

        return brush;
    }
}

/// <summary>
/// Creates the paint for the circle layers of a Mapsforge render theme.
/// </summary>
public static class MFCirclePaint
{
    /// <summary>
    /// Creates a paint from the circle attributes (radius, fill, stroke,
    /// stroke-width) of the layer.
    /// </summary>
    /// <param name="style">The layer style; must be an <see cref="MFCircleLayerStyle"/>.</param>
    /// <returns>The paint for the circle layer.</returns>
    public static Paint CreatePaint(ILayerStyle style)
    {
        if (style is not MFCircleLayerStyle circleStyle)
        {
            throw new ArgumentException($"Style {style.GetType()} isn't a MFCircleLayerStyle");
        }

        var brush = new Paint(style.Name);

        brush.SetFixStyle(PaintStyle.Fill);
        brush.SetFixColor(circleStyle.Fill);
        brush.SetFixRadius(circleStyle.Radius);
        brush.SetFixOutlineColor(circleStyle.Stroke);
        brush.SetFixStrokeWidth(circleStyle.StrokeWidth * MFFillPaint.GetBaseStrokeWidth(circleStyle));

        return brush;
    }
}
