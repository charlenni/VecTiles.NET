using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre;

/// <summary>
/// Creates paints for JSON layer styles, resolving sprite references
/// through the given sprite dictionary.
/// </summary>
public class PaintFactory : IPaintFactory
{
    Func<string, Sprite> _spriteFactory;

    /// <summary>
    /// Initializes the factory with a sprite dictionary used to resolve sprite names.
    /// </summary>
    /// <param name="spriteDictionary">The dictionary containing the sprites referenced by the styles.</param>
    /// <exception cref="ArgumentNullException">Thrown if the dictionary is null.</exception>
    public PaintFactory(SpriteDictionary? spriteDictionary) 
    {
        if (spriteDictionary == null)
            throw new ArgumentNullException(nameof(spriteDictionary));

        _spriteFactory = (name) =>
        {
            var sprite = spriteDictionary.Sprites[name];

            return sprite;
        };
    }

    /// <inheritdoc />
    public IPaint? CreatePaint(ILayerStyle style)
    {
        return style.StyleType switch
        {
            LayerStyleType.Background => BackgroundPaint.CreatePaint(style, _spriteFactory),
            LayerStyleType.Raster => RasterPaint.CreatePaint(style, _spriteFactory),
            LayerStyleType.Fill => FillPaint.CreatePaint(style, _spriteFactory),
            LayerStyleType.Line => LinePaint.CreatePaint(style, _spriteFactory),
            LayerStyleType.Symbol => null,
            LayerStyleType.FillExtrusion => null,
            LayerStyleType.Circle => CirclePaint.CreatePaint(style, _spriteFactory),
            LayerStyleType.Unknown => null,
            _ => throw new NotImplementedException($"Layer style with type '{style.StyleType}' is unknown")
        };
    }
}
