using System.Text.Json.Serialization;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles.Expressions;
using VecTiles.Styles.OpenMapTiles.Json.Converter;

namespace VecTiles.Styles.OpenMapTiles;

public class OMTLayerStylePaint
{
    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("background-color")]
    public StoppedColor BackgroundColor { get; set; } = new StoppedColor { Default = Colors.Black };
    
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("background-emissive-strength")]
    public StoppedFloat BackgroundEmissiveStrength { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("background-opacity")]
    public StoppedFloat BackgroundOpacity { get; set; } = new StoppedFloat() { Default = 1.0f };

    [JsonConverter(typeof(StoppedStringConverter))]
    [JsonPropertyName("background-pattern")]
    public StoppedString BackgroundPattern { get; set; } = new StoppedString() { Default = string.Empty };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("circle-blur")]
    public StoppedFloat CircleBlur { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("circle-color")]
    public StoppedColor CircleColor { get; set; } = new StoppedColor { Default = Colors.Black };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("circle-emissive-strength")]
    public StoppedFloat CircleEmissiveStrength { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("circle-opacity")]
    public StoppedFloat CircleOpacity { get; set; } = new StoppedFloat() { Default = 1.0f };

    [JsonPropertyName("circle-pitch-alignment")]
    public string CirclePitchAlignment { get; set; } = "viewport";

    [JsonPropertyName("circle-pitch-scale")]
    public string CirclePitchScale { get; set; } = "map";

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("circle-radius")]
    public StoppedFloat CircleRadius { get; set; } = new StoppedFloat() { Default = 5.0f };

    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("circle-stroke-color")]
    public StoppedColor CircleStrokeColor { get; set; } = new StoppedColor { Default = Colors.Black };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("circle-stroke-opacity")]
    public StoppedFloat CircleStrokeOpacity { get; set; } = new StoppedFloat() { Default = 1.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("circle-stroke-width")]
    public StoppedFloat CircleStrokeWidth { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("circle-translate")]
    public StoppedFloatArray CircleTranslate { get; set; } = new StoppedFloatArray() { Default = [0, 0] };

    [JsonPropertyName("circle-translate-anchor")]
    public string CircleTranslateAnchor { get; set; } = "map";

    [JsonConverter(typeof(StoppedBooleanConverter))]
    [JsonPropertyName("fill-antialias")]
    public StoppedBoolean FillAntialias { get; set; } = new StoppedBoolean() { Default = true };

    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("fill-color")]
    public StoppedColor FillColor { get; set; } = new StoppedColor { Default = Colors.Black };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("fill-emissive-strength")]
    public StoppedFloat FillEmissiveStrength { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("fill-opacity")]
    public StoppedFloat FillOpacity { get; set; } = new StoppedFloat() { Default = 1.0f };

    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("fill-outline-color")]
    public StoppedColor FillOutlineColor { get; set; } = new StoppedColor() { Default = new Color(0, 0, 0, 0) };

    [JsonConverter(typeof(StoppedStringConverter))]
    [JsonPropertyName("fill-pattern")]
    public StoppedString FillPattern { get; set; } = new StoppedString() { Default = string.Empty };

    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("fill-translate")]
    public StoppedFloatArray FillTranslate { get; set; } = new StoppedFloatArray() { Default = [0, 0] };

    [JsonPropertyName("fill-translate-anchor")]
    public string FillTranslateAnchor { get; set; } = "map";

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-blur")]
    public StoppedFloat LineBlur { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("line-color")]
    public StoppedColor LineColor { get; set; } = new StoppedColor { Default = Colors.Black };

    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("line-dasharray")]
    public StoppedFloatArray LineDashArray { get; set; } = new StoppedFloatArray() { Default = [0, 0] };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-emissive-strength")]
    public StoppedFloat LineEmissiveStrength { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-gap-width")]
    public StoppedFloat LineGapWidth { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("line-gradient")]
    public StoppedColor LineGradient { get; set; } = new StoppedColor() { Default = Colors.Empty };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-occlusion-opacity")]
    public StoppedFloat LineOcclusionOpacity { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-offset")]
    public StoppedFloat LineOffset { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-opacity")]
    public StoppedFloat LineOpacity { get; set; } = new StoppedFloat() { Default = 1.0f };

    [JsonConverter(typeof(StoppedStringConverter))]
    [JsonPropertyName("line-pattern")]
    public StoppedString LinePattern { get; set; } = new StoppedString { Default = string.Empty };

    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("line-translate")]
    public StoppedFloatArray LineTranslate { get; set; } = new StoppedFloatArray() { Default = [0, 0] };

    [JsonPropertyName("line-translate-anchor")]
    public string LineTranslateAnchor { get; set; } = "map";

    [JsonPropertyName("line-trim-offset")]
    public float[] LineTrimOffset { get; set; } = [0, 0];

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-width")]
    public StoppedFloat LineWidth { get; set; } = new StoppedFloat() { Default = 1.0f };

    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("icon-color")]
    public StoppedColor IconColor { get; set; } = new StoppedColor { Default = Colors.Black };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-color-brightness-max")]
    public StoppedFloat IconColorBrightnessMax { get; set; } = new StoppedFloat { Default = 1.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-color-brightness-min")]
    public StoppedFloat IconColorBrightnessMin { get; set; } = new StoppedFloat { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-color-contrast")]
    public StoppedFloat IconColorContrast { get; set; } = new StoppedFloat { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-color-saturation")]
    public StoppedFloat IconColorSaturation { get; set; } = new StoppedFloat { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-emissive-strength")]
    public StoppedFloat IconEmissiveStrength { get; set; } = new StoppedFloat() { Default = 1.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-halo-blur")]
    public StoppedFloat IconHaloBlur { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("icon-halo-color")]
    public StoppedColor IconHaloColor { get; set; } = new StoppedColor { Default = Colors.Empty };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-halo-width")]
    public StoppedFloat IconHaloWidth { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-image-cross-fade")]
    public StoppedFloat IconImageCrossFade { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-occlusion-opacity")]
    public StoppedFloat IconOcclusionOpacity { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-opacity")]
    public StoppedFloat IconOpacity { get; set; } = new StoppedFloat() { Default = 1.0f };

    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("icon-translate")]
    public StoppedFloatArray IconTranslate { get; set; } = new StoppedFloatArray() { Default = [0, 0] };

    [JsonConverter(typeof(StoppedEnumConverter<MapAlignment>))]
    [JsonPropertyName("icon-translate-anchor")]
    public StoppedEnum<MapAlignment> IconTranslateAnchor { get; set; } = new StoppedEnum<MapAlignment> { Default = MapAlignment.Map };

    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("text-color")]
    public StoppedColor TextColor { get; set; } = new StoppedColor { Default = Colors.Black };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-emissive-strength")]
    public StoppedFloat TextEmissiveStrength { get; set; } = new StoppedFloat() { Default = 1.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-halo-blur")]
    public StoppedFloat TextHaloBlur { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("text-halo-color")]
    public StoppedColor TextHaloColor { get; set; } = new StoppedColor { Default = Colors.Empty };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-halo-width")]
    public StoppedFloat TextHaloWidth { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-occlusion-opacity")]
    public StoppedFloat TextOcclusionOpacity { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-opacity")]
    public StoppedFloat TextOpacity { get; set; } = new StoppedFloat { Default = 1.0f };

    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("text-translate")]
    public StoppedFloatArray TextTranslate { get; set; } = new StoppedFloatArray() { Default = [0, 0] };

    [JsonConverter(typeof(StoppedEnumConverter<MapAlignment>))]
    [JsonPropertyName("text-translate-anchor")]
    public StoppedEnum<MapAlignment> TextTranslateAnchor { get; set; } = new StoppedEnum<MapAlignment> { Default = MapAlignment.Map };

    [JsonPropertyName("raster-brightness-max")]
    public StoppedFloat RasterBrightnessMax { get; set; } = new StoppedFloat { Default = 1.0f };

    [JsonPropertyName("raster-brightness-min")]
    public StoppedFloat RasterBrightnessMin { get; set; } = new StoppedFloat { Default = 0.0f };

    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("raster-color")]
    public StoppedColor RasterColor { get; set; } = new StoppedColor() { Default = Colors.Empty };

    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("raster-color-mix")]
    public StoppedFloatArray RasterColorMix { get; set; } = new StoppedFloatArray { Default = [0.2126f, 0.7152f, 0.0722f, 0.0f] };

    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("raster-color-range")]
    public StoppedFloatArray RasterColorRange { get; set; } = new StoppedFloatArray() { Default = [] };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("raster-contrast")]
    public StoppedFloat RasterContrast { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("raster-emissive-strength")]
    public StoppedFloat RasterEmissiveStrength { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("raster-fade-duration")]
    public StoppedFloat RasterFadeDuration { get; set; } = new StoppedFloat() { Default = 300.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("raster-hue-rotate")]
    public StoppedFloat RasterHueRotate { get; set; } = new StoppedFloat() { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("raster-opacity")]
    public StoppedFloat RasterOpacity { get; set; } = new StoppedFloat() { Default = 1.0f };

    [JsonPropertyName("raster-resampling")]
    public string RasterResampling { get; set; } = "linear";

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("raster-saturation")]
    public StoppedFloat RasterSaturation { get; set; } = new StoppedFloat() { Default = 0.0f };
}
