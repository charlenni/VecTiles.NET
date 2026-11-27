using System.Text.Json.Serialization;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.Styles.MapLibre.Expressions;
using VecTiles.Styles.MapLibre.Json.Converter;

namespace VecTiles.Styles.MapLibre;

/// <summary>
/// Class holding the paint properties of a JSON style layer, as present
/// in the JSON style file.
/// </summary>
public class LayerStylePaint
{
    /// <summary>
    /// The color with which the background is drawn.
    /// </summary>
    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("background-color")]
    public StoppedColor BackgroundColor { get; set; } = new StoppedColor { Default = Colors.Black };
    
    /// <summary>
    /// Intensity with which the background emits light (3D rendering only).
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("background-emissive-strength")]
    public StoppedFloat BackgroundEmissiveStrength { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// The opacity at which the background is drawn.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("background-opacity")]
    public StoppedFloat BackgroundOpacity { get; set; } = new StoppedFloat() { Default = 1.0f };

    /// <summary>
    /// Name of the sprite image used for drawing the background.
    /// </summary>
    [JsonConverter(typeof(StoppedStringConverter))]
    [JsonPropertyName("background-pattern")]
    public StoppedString BackgroundPattern { get; set; } = new StoppedString() { Default = string.Empty };

    /// <summary>
    /// Amount to blur the circle; 1 blurs the circle so that only the centerpoint
    /// has full opacity.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("circle-blur")]
    public StoppedFloat CircleBlur { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// The fill color of the circle.
    /// </summary>
    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("circle-color")]
    public StoppedColor CircleColor { get; set; } = new StoppedColor { Default = Colors.Black };

    /// <summary>
    /// Intensity with which the circle emits light (3D rendering only).
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("circle-emissive-strength")]
    public StoppedFloat CircleEmissiveStrength { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// The opacity at which the circle is drawn.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("circle-opacity")]
    public StoppedFloat CircleOpacity { get; set; } = new StoppedFloat() { Default = 1.0f };

    /// <summary>
    /// Orientation of the circle when the map is pitched; map or viewport.
    /// </summary>
    [JsonPropertyName("circle-pitch-alignment")]
    public string CirclePitchAlignment { get; set; } = "viewport";

    /// <summary>
    /// How the circle is scaled when the map is pitched; map or viewport.
    /// </summary>
    [JsonPropertyName("circle-pitch-scale")]
    public string CirclePitchScale { get; set; } = "map";

    /// <summary>
    /// Circle radius, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("circle-radius")]
    public StoppedFloat CircleRadius { get; set; } = new StoppedFloat() { Default = 5.0f };

    /// <summary>
    /// The stroke color of the circle.
    /// </summary>
    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("circle-stroke-color")]
    public StoppedColor CircleStrokeColor { get; set; } = new StoppedColor { Default = Colors.Black };

    /// <summary>
    /// The opacity of the circle's stroke.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("circle-stroke-opacity")]
    public StoppedFloat CircleStrokeOpacity { get; set; } = new StoppedFloat() { Default = 1.0f };

    /// <summary>
    /// The width of the circle's stroke, in pixels; placed outside the circle radius.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("circle-stroke-width")]
    public StoppedFloat CircleStrokeWidth { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// Offset of the circle geometry, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("circle-translate")]
    public StoppedFloatArray CircleTranslate { get; set; } = new StoppedFloatArray() { Default = [0, 0] };

    /// <summary>
    /// Whether the circle translation is relative to the map or the viewport.
    /// </summary>
    [JsonPropertyName("circle-translate-anchor")]
    public string CircleTranslateAnchor { get; set; } = "map";

    /// <summary>
    /// Whether the fill is antialiased.
    /// </summary>
    [JsonConverter(typeof(StoppedBooleanConverter))]
    [JsonPropertyName("fill-antialias")]
    public StoppedBoolean FillAntialias { get; set; } = new StoppedBoolean() { Default = true };

    /// <summary>
    /// The color of the filled part of the layer.
    /// </summary>
    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("fill-color")]
    public StoppedColor FillColor { get; set; } = new StoppedColor { Default = Colors.Black };

    /// <summary>
    /// Intensity with which the fill emits light (3D rendering only).
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("fill-emissive-strength")]
    public StoppedFloat FillEmissiveStrength { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// The opacity of the entire fill layer.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("fill-opacity")]
    public StoppedFloat FillOpacity { get; set; } = new StoppedFloat() { Default = 1.0f };

    /// <summary>
    /// The outline color of the fill; defaults to fill-color when unspecified.
    /// </summary>
    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("fill-outline-color")]
    public StoppedColor FillOutlineColor { get; set; } = new StoppedColor() { Default = new Color(0, 0, 0, 0) };

    /// <summary>
    /// Name of the sprite image used for drawing image fills.
    /// </summary>
    [JsonConverter(typeof(StoppedStringConverter))]
    [JsonPropertyName("fill-pattern")]
    public StoppedString FillPattern { get; set; } = new StoppedString() { Default = string.Empty };

    /// <summary>
    /// Offset of the fill geometry, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("fill-translate")]
    public StoppedFloatArray FillTranslate { get; set; } = new StoppedFloatArray() { Default = [0, 0] };

    /// <summary>
    /// Whether the fill translation is relative to the map or the viewport.
    /// </summary>
    [JsonPropertyName("fill-translate-anchor")]
    public string FillTranslateAnchor { get; set; } = "map";

    /// <summary>
    /// Blur applied to the line, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-blur")]
    public StoppedFloat LineBlur { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// The color with which the line is drawn.
    /// </summary>
    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("line-color")]
    public StoppedColor LineColor { get; set; } = new StoppedColor { Default = Colors.Black };

    /// <summary>
    /// Lengths of the alternating dashes and gaps forming the dash pattern,
    /// scaled by the line width.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("line-dasharray")]
    public StoppedFloatArray LineDashArray { get; set; } = new StoppedFloatArray() { Default = [0, 0] };

    /// <summary>
    /// Intensity with which the line emits light (3D rendering only).
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-emissive-strength")]
    public StoppedFloat LineEmissiveStrength { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// Width of a line casing drawn outside the line's actual path, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-gap-width")]
    public StoppedFloat LineGapWidth { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// A gradient color used to color a line along its length.
    /// </summary>
    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("line-gradient")]
    public StoppedColor LineGradient { get; set; } = new StoppedColor() { Default = Colors.Empty };

    /// <summary>
    /// Opacity applied to parts of the line occluded by 3D features.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-occlusion-opacity")]
    public StoppedFloat LineOcclusionOpacity { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// The line's offset perpendicular to its direction, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-offset")]
    public StoppedFloat LineOffset { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// The opacity at which the line is drawn.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-opacity")]
    public StoppedFloat LineOpacity { get; set; } = new StoppedFloat() { Default = 1.0f };

    /// <summary>
    /// Name of the sprite image used for drawing image lines.
    /// </summary>
    [JsonConverter(typeof(StoppedStringConverter))]
    [JsonPropertyName("line-pattern")]
    public StoppedString LinePattern { get; set; } = new StoppedString { Default = string.Empty };

    /// <summary>
    /// Offset of the line geometry, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("line-translate")]
    public StoppedFloatArray LineTranslate { get; set; } = new StoppedFloatArray() { Default = [0, 0] };

    /// <summary>
    /// Whether the line translation is relative to the map or the viewport.
    /// </summary>
    [JsonPropertyName("line-translate-anchor")]
    public string LineTranslateAnchor { get; set; } = "map";

    /// <summary>
    /// Start and end stop positions of the trimmed (hidden) part of the line.
    /// </summary>
    [JsonPropertyName("line-trim-offset")]
    public float[] LineTrimOffset { get; set; } = [0, 0];

    /// <summary>
    /// Stroke thickness, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-width")]
    public StoppedFloat LineWidth { get; set; } = new StoppedFloat() { Default = 1.0f };

    /// <summary>
    /// The color with which SDF icons are tinted.
    /// </summary>
    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("icon-color")]
    public StoppedColor IconColor { get; set; } = new StoppedColor { Default = Colors.Black };

    /// <summary>
    /// Maximum brightness of the icon color, in the range 0 to 1.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-color-brightness-max")]
    public StoppedFloat IconColorBrightnessMax { get; set; } = new StoppedFloat { Default = 1.0f };

    /// <summary>
    /// Minimum brightness of the icon color, in the range 0 to 1.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-color-brightness-min")]
    public StoppedFloat IconColorBrightnessMin { get; set; } = new StoppedFloat { Default = 0.0f };

    /// <summary>
    /// Contrast adjustment of the icon color, in the range -1 to 1.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-color-contrast")]
    public StoppedFloat IconColorContrast { get; set; } = new StoppedFloat { Default = 0.0f };

    /// <summary>
    /// Saturation adjustment of the icon color, in the range -1 to 1.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-color-saturation")]
    public StoppedFloat IconColorSaturation { get; set; } = new StoppedFloat { Default = 0.0f };

    /// <summary>
    /// Intensity with which the icon emits light (3D rendering only).
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-emissive-strength")]
    public StoppedFloat IconEmissiveStrength { get; set; } = new StoppedFloat() { Default = 1.0f };

    /// <summary>
    /// Blur applied to the icon halo, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-halo-blur")]
    public StoppedFloat IconHaloBlur { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// The color of the icon halo.
    /// </summary>
    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("icon-halo-color")]
    public StoppedColor IconHaloColor { get; set; } = new StoppedColor { Default = Colors.Empty };

    /// <summary>
    /// Width of the icon halo, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-halo-width")]
    public StoppedFloat IconHaloWidth { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// Fade progress between two icon images when transitioning, in the range 0 to 1.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-image-cross-fade")]
    public StoppedFloat IconImageCrossFade { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// Opacity applied to parts of the icon occluded by 3D features.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-occlusion-opacity")]
    public StoppedFloat IconOcclusionOpacity { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// The opacity at which the icon is drawn.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-opacity")]
    public StoppedFloat IconOpacity { get; set; } = new StoppedFloat() { Default = 1.0f };

    /// <summary>
    /// Offset of the icon, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("icon-translate")]
    public StoppedFloatArray IconTranslate { get; set; } = new StoppedFloatArray() { Default = [0, 0] };

    /// <summary>
    /// Whether the icon translation is relative to the map or the viewport.
    /// </summary>
    [JsonConverter(typeof(StoppedEnumConverter<MapAlignment>))]
    [JsonPropertyName("icon-translate-anchor")]
    public StoppedEnum<MapAlignment> IconTranslateAnchor { get; set; } = new StoppedEnum<MapAlignment> { Default = MapAlignment.Map };

    /// <summary>
    /// The color with which the text is drawn.
    /// </summary>
    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("text-color")]
    public StoppedColor TextColor { get; set; } = new StoppedColor { Default = Colors.Black };

    /// <summary>
    /// Intensity with which the text emits light (3D rendering only).
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-emissive-strength")]
    public StoppedFloat TextEmissiveStrength { get; set; } = new StoppedFloat() { Default = 1.0f };

    /// <summary>
    /// Blur applied to the text halo, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-halo-blur")]
    public StoppedFloat TextHaloBlur { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// The color of the text halo.
    /// </summary>
    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("text-halo-color")]
    public StoppedColor TextHaloColor { get; set; } = new StoppedColor { Default = Colors.Empty };

    /// <summary>
    /// Width of the text halo, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-halo-width")]
    public StoppedFloat TextHaloWidth { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// Opacity applied to parts of the text occluded by 3D features.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-occlusion-opacity")]
    public StoppedFloat TextOcclusionOpacity { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// The opacity at which the text is drawn.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-opacity")]
    public StoppedFloat TextOpacity { get; set; } = new StoppedFloat { Default = 1.0f };

    /// <summary>
    /// Offset of the text, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("text-translate")]
    public StoppedFloatArray TextTranslate { get; set; } = new StoppedFloatArray() { Default = [0, 0] };

    /// <summary>
    /// Whether the text translation is relative to the map or the viewport.
    /// </summary>
    [JsonConverter(typeof(StoppedEnumConverter<MapAlignment>))]
    [JsonPropertyName("text-translate-anchor")]
    public StoppedEnum<MapAlignment> TextTranslateAnchor { get; set; } = new StoppedEnum<MapAlignment> { Default = MapAlignment.Map };

    /// <summary>
    /// Maximum brightness of the raster layer, in the range 0 to 1.
    /// </summary>
    [JsonPropertyName("raster-brightness-max")]
    public StoppedFloat RasterBrightnessMax { get; set; } = new StoppedFloat { Default = 1.0f };

    /// <summary>
    /// Minimum brightness of the raster layer, in the range 0 to 1.
    /// </summary>
    [JsonPropertyName("raster-brightness-min")]
    public StoppedFloat RasterBrightnessMin { get; set; } = new StoppedFloat { Default = 0.0f };

    /// <summary>
    /// Color to use for coloring the raster image.
    /// </summary>
    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("raster-color")]
    public StoppedColor RasterColor { get; set; } = new StoppedColor() { Default = Colors.Empty };

    /// <summary>
    /// Mixing weights used to color the raster image.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("raster-color-mix")]
    public StoppedFloatArray RasterColorMix { get; set; } = new StoppedFloatArray { Default = [0.2126f, 0.7152f, 0.0722f, 0.0f] };

    /// <summary>
    /// Range of the input values mapped to the raster color.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("raster-color-range")]
    public StoppedFloatArray RasterColorRange { get; set; } = new StoppedFloatArray() { Default = [] };

    /// <summary>
    /// Contrast adjustment of the raster layer, in the range -1 to 1.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("raster-contrast")]
    public StoppedFloat RasterContrast { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// Intensity with which the raster layer emits light (3D rendering only).
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("raster-emissive-strength")]
    public StoppedFloat RasterEmissiveStrength { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// Fade duration when a new tile is added, in milliseconds.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("raster-fade-duration")]
    public StoppedFloat RasterFadeDuration { get; set; } = new StoppedFloat() { Default = 300.0f };

    /// <summary>
    /// Hue rotation of the raster layer, in degrees.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("raster-hue-rotate")]
    public StoppedFloat RasterHueRotate { get; set; } = new StoppedFloat() { Default = 0.0f };

    /// <summary>
    /// The opacity at which the raster image is drawn.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("raster-opacity")]
    public StoppedFloat RasterOpacity { get; set; } = new StoppedFloat() { Default = 1.0f };

    /// <summary>
    /// Resampling applied when scaling the raster image; "linear" or "nearest".
    /// </summary>
    [JsonPropertyName("raster-resampling")]
    public string RasterResampling { get; set; } = "linear";

    /// <summary>
    /// Saturation adjustment of the raster layer, in the range -1 to 1.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("raster-saturation")]
    public StoppedFloat RasterSaturation { get; set; } = new StoppedFloat() { Default = 0.0f };
}
