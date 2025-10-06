using Newtonsoft.Json;
using VecTiles.Common.Enums;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapbox.Enums;
using VecTiles.Styles.Mapbox.Expressions;
using VecTiles.Styles.Mapbox.Json.Converter;

namespace VecTiles.Styles.Mapbox;

/// <summary>
/// Class holding Layout data in Json format
/// </summary>
public class MapboxLayerStyleLayout
{
    [JsonProperty("visibility")]
    public string Visibility { get; set; } = "visible";

    [JsonProperty("fill-sort-key")]
    public float? FillSortKey { get; set; }

    [JsonProperty("line-cap")]
    public string LineCap { get; set; } = "butt";

    [JsonProperty("line-join")]
    public string LineJoin { get; set; } = "miter";

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonProperty("line-miter-limit")]
    public StoppedFloat LineMiterLimit { get; set; } = new StoppedFloat { Default = 2.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonProperty("line-round-limit")]
    public StoppedFloat LineRoundLimit { get; set; } = new StoppedFloat { Default = 1.05f };

    [JsonProperty("line-sort-key")]
    public float? LineSortKey { get; set; }

    [JsonProperty("icon-allow-overlap")]
    public bool IconAllowOverlap { get; set; } = false;

    [JsonConverter(typeof(EnumConverter<Anchor>))]
    [JsonProperty("icon-anchor")]
    public Anchor IconAnchor { get; set; } = Anchor.Center;

    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonProperty("icon-color")]
    public StoppedColor IconColor { get; set; } = new StoppedColor { Default = Color.Black };

    [JsonProperty("icon-ignore-placement")]
    public bool IconIgnorePlacement { get; set; } = false;

    [JsonConverter(typeof(StoppedStringConverter))]
    [JsonProperty("icon-image")]
    public StoppedString IconImage { get; set; } = new StoppedString() { Default = string.Empty };

    [JsonProperty("icon-keep-upright")]
    public bool IconKeepUpright { get; set; } = false;

    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonProperty("icon-offset")]
    public StoppedFloatArray IconOffset { get; set; } = new StoppedFloatArray { Default = [0, 0] };

    [JsonProperty("icon-optional")]
    public bool IconOptional { get; set; } = false;

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonProperty("icon-padding")]
    public StoppedFloat IconPadding { get; set; } = new StoppedFloat { Default = 2.0f };

    [JsonConverter(typeof(EnumConverter<MapAlignment>))]
    [JsonProperty("icon-pitch-alignment")]
    public MapAlignment IconPitchAlignment { get; set; } = MapAlignment.Auto;

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonProperty("icon-rotate")]
    public StoppedFloat IconRotate { get; set; } = new StoppedFloat { Default = 0.0f };

    [JsonConverter(typeof(EnumConverter<MapAlignment>))]
    [JsonProperty("icon-rotation-alignment")]
    public MapAlignment IconRotationAlignment { get; set; } = MapAlignment.Auto;

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonProperty("icon-size")]
    public StoppedFloat IconSize { get; set; } = new StoppedFloat { Default = 1.0f };

    [JsonProperty("icon-text-fit")]
    public string IconTextFit { get; set; } = "none";

    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonProperty("icon-text-fit-padding")]
    public StoppedFloatArray IconTextFitPadding { get; set; } = new StoppedFloatArray { Default = [0, 0, 0, 0] };

    [JsonProperty("symbol-avoid-edges")]
    public bool SymbolAvoidEdges { get; set; } = false;

    [JsonConverter(typeof(StoppedEnumConverter<SymbolPlacement>))]
    [JsonProperty("symbol-placement")]
    public StoppedEnum<SymbolPlacement> SymbolPlacement { get; set; } = new StoppedEnum<SymbolPlacement> { Default = Common.Enums.SymbolPlacement.Point };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonProperty("symbol-sort-key")]
    public StoppedFloat SymbolSortKey { get; set; } = new StoppedFloat() { Default = 0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonProperty("symbol-spacing")]
    public StoppedFloat SymbolSpacing { get; set; } = new StoppedFloat { Default = 250.0f };

    [JsonProperty("symbol-z-elevate")]
    public bool SymbolZElevate { get; set; } = false;

    [JsonConverter(typeof(EnumConverter<SymbolZOrder>))]
    [JsonProperty("symbol-z-order")]
    public SymbolZOrder SymbolZOrder { get; set; } = SymbolZOrder.Auto;

    [JsonProperty("text-allow-overlap")]
    public bool TextAllowOverlap { get; set; } = false;

    [JsonConverter(typeof(EnumConverter<Anchor>))]
    [JsonProperty("text-anchor")]
    public Anchor TextAnchor { get; set; } = Anchor.Center;

    [JsonProperty("text-field")]
    public string TextField { get; set; } = string.Empty;

    [JsonProperty("text-font")]
    public string[] TextFont { get; set; } = ["Open Sans Regular", "Arial Unicode MS Regular"];

    [JsonProperty("text-ignore-placement")]
    public bool TextIgnorePlacement { get; set; } = false;

    [JsonConverter(typeof(EnumConverter<TextJustify>))]
    [JsonProperty("text-justify")]
    public TextJustify TextJustify { get; set; } = TextJustify.Auto;

    [JsonProperty("text-keep-upright")]
    public bool TextKeepUpright { get; set; } = true;

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonProperty("text-letter-spacing")]
    public StoppedFloat TextLetterSpacing { get; set; } = new StoppedFloat { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonProperty("text-line-height")]
    public StoppedFloat TextLineHeight { get; set; } = new StoppedFloat { Default = 1.2f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonProperty("text-max-angle")]
    public StoppedFloat TextMaxAngle { get; set; } = new StoppedFloat { Default = 45.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonProperty("text-max-width")]
    public StoppedFloat TextMaxWidth { get; set; } = new StoppedFloat { Default = 10.0f };

    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonProperty("text-offset")]
    public StoppedFloatArray TextOffset { get; set; } = new StoppedFloatArray { Default = [0, 0] };

    [JsonProperty("text-optional")]
    public bool TextOptional { get; set; } = false;

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonProperty("text-padding")]
    public StoppedFloat TextPadding { get; set; } = new StoppedFloat { Default = 2.0f };

    [JsonProperty("text-pitch-alignment")]
    public string TextPitchAlignment { get; set; } = "auto";

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonProperty("text-radial-offset")]
    public StoppedFloat TextRadialOffset { get; set; } = new StoppedFloat { Default = 0.0f };

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonProperty("text-rotate")]
    public StoppedFloat TextRotate { get; set; } = new StoppedFloat { Default = 0.0f };

    [JsonProperty("text-rotation-alignment")]
    public string TextRotationAlignment { get; set; } = "auto";

    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonProperty("text-size")]
    public StoppedFloat TextSize { get; set; } = new StoppedFloat { Default = 16.0f };

    [JsonConverter(typeof(EnumConverter<TextTransform>))]
    [JsonProperty("text-transform")]
    public TextTransform TextTransform { get; set; } = TextTransform.None;

    [JsonProperty("text-variable-anchor")]
    public string[] TextVariableAnchor { get; set; } = [];

    [JsonProperty("text-writing-mode")]
    public string[] TextWritingMode { get; set; } = [];

    public static MapboxLayerStyleLayout Empty = new MapboxLayerStyleLayout();
}
