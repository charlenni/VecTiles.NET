using System.Text.Json.Serialization;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles.Enums;
using VecTiles.Styles.OpenMapTiles.Expressions;
using VecTiles.Styles.OpenMapTiles.Json.Converter;

namespace VecTiles.Styles.OpenMapTiles;

/// <summary>
/// Class holding Layout data in Json format
/// </summary>
public class OMTLayerStyleLayout
{
    [JsonPropertyName("visibility")] public string Visibility { get; set; } = "visible";

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("circle-sort-key")]
    public StoppedFloat CircleSortKey { get; set; } = new StoppedFloat() { Default = 0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("fill-sort-key")]
    public StoppedFloat FillSortKey { get; set; } = new StoppedFloat() { Default = 0f };

    [JsonPropertyName("line-cap")] public string LineCap { get; set; } = "butt";

    [JsonPropertyName("line-join")] public string LineJoin { get; set; } = "miter";

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-miter-limit")]
    public StoppedFloat LineMiterLimit { get; set; } = new StoppedFloat { Default = 2.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-round-limit")]
    public StoppedFloat LineRoundLimit { get; set; } = new StoppedFloat { Default = 1.05f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-sort-key")]
    public StoppedFloat LineSortKey { get; set; } = new StoppedFloat() { Default = 0f };

    [JsonPropertyName("icon-allow-overlap")] public bool IconAllowOverlap { get; set; } = false;

    [JsonConverter(typeof(EnumConverter<Anchor>))]
    [JsonPropertyName("icon-anchor")]
    public Anchor IconAnchor { get; set; } = Anchor.Center;

    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("icon-color")]
    public StoppedColor IconColor { get; set; } = new StoppedColor { Default = Colors.Black };

    [JsonPropertyName("icon-ignore-placement")]
    public bool IconIgnorePlacement { get; set; } = false;

    [JsonConverter(typeof(StoppedStringConverter))]
    [JsonPropertyName("icon-image")]
    public StoppedString IconImage { get; set; } = new StoppedString() { Default = string.Empty };

    [JsonPropertyName("icon-keep-upright")] public bool IconKeepUpright { get; set; } = false;

    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("icon-offset")]
    public StoppedFloatArray IconOffset { get; set; } = new StoppedFloatArray { Default = [0, 0] };

    [JsonPropertyName("icon-optional")] public bool IconOptional { get; set; } = false;

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-padding")]
    public StoppedFloat IconPadding { get; set; } = new StoppedFloat { Default = 2.0f };

    [JsonConverter(typeof(EnumConverter<MapAlignment>))]
    [JsonPropertyName("icon-pitch-alignment")]
    public MapAlignment IconPitchAlignment { get; set; } = MapAlignment.Auto;

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-rotate")]
    public StoppedFloat IconRotate { get; set; } = new StoppedFloat { Default = 0.0f };

    [JsonConverter(typeof(EnumConverter<MapAlignment>))]
    [JsonPropertyName("icon-rotation-alignment")]
    public MapAlignment IconRotationAlignment { get; set; } = MapAlignment.Auto;

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-size")]
    public StoppedFloat IconSize { get; set; } = new StoppedFloat { Default = 1.0f };

    [JsonPropertyName("icon-text-fit")] public string IconTextFit { get; set; } = "none";

    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("icon-text-fit-padding")]
    public StoppedFloatArray IconTextFitPadding { get; set; } = new StoppedFloatArray { Default = [0, 0, 0, 0] };

    [JsonPropertyName("symbol-avoid-edges")] public bool SymbolAvoidEdges { get; set; } = false;

    [JsonConverter(typeof(StoppedEnumConverter<SymbolPlacement>))]
    [JsonPropertyName("symbol-placement")]
    public StoppedEnum<SymbolPlacement> SymbolPlacement { get; set; } = new StoppedEnum<SymbolPlacement>
        { Default = Common.Enums.SymbolPlacement.Point };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("symbol-sort-key")]
    public StoppedFloat SymbolSortKey { get; set; } = new StoppedFloat() { Default = 0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("symbol-spacing")]
    public StoppedFloat SymbolSpacing { get; set; } = new StoppedFloat { Default = 250.0f };

    [JsonPropertyName("symbol-z-elevate")] public bool SymbolZElevate { get; set; } = false;

    [JsonConverter(typeof(EnumConverter<SymbolZOrder>))]
    [JsonPropertyName("symbol-z-order")]
    public SymbolZOrder SymbolZOrder { get; set; } = SymbolZOrder.Auto;

    [JsonConverter(typeof(StoppedBooleanConverter))]
    [JsonPropertyName("text-allow-overlap")]
    public StoppedBoolean TextAllowOverlap { get; set; } = new StoppedBoolean() { Default = false };

    [JsonConverter(typeof(EnumConverter<Anchor>))]
    [JsonPropertyName("text-anchor")]
    public Anchor TextAnchor { get; set; } = Anchor.Center;

    [JsonConverter(typeof(StoppedStringConverter))]
    [JsonPropertyName("text-field")]
    public StoppedString TextField { get; set; } = new StoppedString() { Default =  string.Empty };

    [JsonPropertyName("text-font")]
    // Empty = the style does not specify a font; OMTTextBuilder falls back to
    // DefaultFontNames on its own, so that only really used fonts are reported
    public string[] TextFont { get; set; } = [];

    [JsonConverter(typeof(StoppedBooleanConverter))]
    [JsonPropertyName("text-ignore-placement")]
    public StoppedBoolean TextIgnorePlacement { get; set; } = new StoppedBoolean() { Default = false };

    [JsonConverter(typeof(EnumConverter<TextJustify>))]
    [JsonPropertyName("text-justify")]
    public TextJustify TextJustify { get; set; } = TextJustify.Auto;

    [JsonPropertyName("text-keep-upright")]
    public bool TextKeepUpright { get; set; } = true;

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-letter-spacing")]
    public StoppedFloat TextLetterSpacing { get; set; } = new StoppedFloat { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-line-height")]
    public StoppedFloat TextLineHeight { get; set; } = new StoppedFloat { Default = 1.2f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-max-angle")]
    public StoppedFloat TextMaxAngle { get; set; } = new StoppedFloat { Default = 45.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-max-width")]
    public StoppedFloat TextMaxWidth { get; set; } = new StoppedFloat { Default = 10.0f };

    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("text-offset")]
    public StoppedFloatArray TextOffset { get; set; } = new StoppedFloatArray { Default = [0, 0] };

    [JsonPropertyName("text-optional")]
    public bool TextOptional { get; set; } = false;

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-padding")]
    public StoppedFloat TextPadding { get; set; } = new StoppedFloat { Default = 2.0f };

    [JsonPropertyName("text-pitch-alignment")]
    public string TextPitchAlignment { get; set; } = "auto";

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-radial-offset")]
    public StoppedFloat TextRadialOffset { get; set; } = new StoppedFloat { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-rotate")]
    public StoppedFloat TextRotate { get; set; } = new StoppedFloat { Default = 0.0f };

    [JsonConverter(typeof(EnumConverter<MapAlignment>))]
    [JsonPropertyName("text-rotation-alignment")]
    public MapAlignment TextRotationAlignment { get; set; }  = MapAlignment.Auto;

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-size")]
    public StoppedFloat TextSize { get; set; } = new StoppedFloat { Default = 16.0f };

    [JsonConverter(typeof(StoppedEnumConverter<TextTransform>))]
    [JsonPropertyName("text-transform")]
    public StoppedEnum<TextTransform> TextTransform { get; set; } = new StoppedEnum<TextTransform>() { Default = Common.Enums.TextTransform.None };

    [JsonPropertyName("text-variable-anchor")]
    public string[] TextVariableAnchor { get; set; } = [];

    [JsonPropertyName("text-writing-mode")]
    public string[] TextWritingMode { get; set; } = [];

    public static OMTLayerStyleLayout Empty = new OMTLayerStyleLayout();
}
