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
    /// <summary>
    /// Visibility of the layer; "visible" or "none".
    /// </summary>
    [JsonPropertyName("visibility")] public string Visibility { get; set; } = "visible";

    /// <summary>
    /// Sorts features in ascending order based on this value for circle layers.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("circle-sort-key")]
    public StoppedFloat CircleSortKey { get; set; } = new StoppedFloat() { Default = 0f };

    /// <summary>
    /// Sorts features in ascending order based on this value for fill layers.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("fill-sort-key")]
    public StoppedFloat FillSortKey { get; set; } = new StoppedFloat() { Default = 0f };

    /// <summary>
    /// The display of line endings; "butt", "round", or "square".
    /// </summary>
    [JsonPropertyName("line-cap")] public string LineCap { get; set; } = "butt";

    /// <summary>
    /// The display of lines when joining; "bevel", "round", or "miter".
    /// </summary>
    [JsonPropertyName("line-join")] public string LineJoin { get; set; } = "miter";

    /// <summary>
    /// Used to automatically convert miter joins to bevel joins for sharp angles.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-miter-limit")]
    public StoppedFloat LineMiterLimit { get; set; } = new StoppedFloat { Default = 2.0f };

    /// <summary>
    /// Used to automatically convert round joins to miter joins for shallow angles.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-round-limit")]
    public StoppedFloat LineRoundLimit { get; set; } = new StoppedFloat { Default = 1.05f };

    /// <summary>
    /// Sorts features in ascending order based on this value for line layers.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("line-sort-key")]
    public StoppedFloat LineSortKey { get; set; } = new StoppedFloat() { Default = 0f };

    /// <summary>
    /// Whether the icon may be placed overlapping other symbols.
    /// </summary>
    [JsonPropertyName("icon-allow-overlap")] public bool IconAllowOverlap { get; set; } = false;

    /// <summary>
    /// Part of the icon placed closest to the anchor position.
    /// </summary>
    [JsonConverter(typeof(EnumConverter<Anchor>))]
    [JsonPropertyName("icon-anchor")]
    public Anchor IconAnchor { get; set; } = Anchor.Center;

    /// <summary>
    /// The color with which SDF icons are tinted.
    /// </summary>
    [JsonConverter(typeof(StoppedColorConverter))]
    [JsonPropertyName("icon-color")]
    public StoppedColor IconColor { get; set; } = new StoppedColor { Default = Colors.Black };

    /// <summary>
    /// Whether other symbols may be placed overlapping the icon, ignoring collision.
    /// </summary>
    [JsonPropertyName("icon-ignore-placement")]
    public bool IconIgnorePlacement { get; set; } = false;

    /// <summary>
    /// Name of the sprite image used for the icon; supports field placeholders.
    /// </summary>
    [JsonConverter(typeof(StoppedStringConverter))]
    [JsonPropertyName("icon-image")]
    public StoppedString IconImage { get; set; } = new StoppedString() { Default = string.Empty };

    /// <summary>
    /// Whether the icon is kept upright when placed on a line.
    /// </summary>
    [JsonPropertyName("icon-keep-upright")] public bool IconKeepUpright { get; set; } = false;

    /// <summary>
    /// Offset of the icon relative to its anchor position, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("icon-offset")]
    public StoppedFloatArray IconOffset { get; set; } = new StoppedFloatArray { Default = [0, 0] };

    /// <summary>
    /// Whether the icon is optional (hidden when it overlaps other symbols
    /// while the text is still shown).
    /// </summary>
    [JsonPropertyName("icon-optional")] public bool IconOptional { get; set; } = false;

    /// <summary>
    /// Extra space around the icon used for collision detection, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-padding")]
    public StoppedFloat IconPadding { get; set; } = new StoppedFloat { Default = 2.0f };

    /// <summary>
    /// Orientation of the icon when the map is pitched; map, viewport, or auto.
    /// </summary>
    [JsonConverter(typeof(EnumConverter<MapAlignment>))]
    [JsonPropertyName("icon-pitch-alignment")]
    public MapAlignment IconPitchAlignment { get; set; } = MapAlignment.Auto;

    /// <summary>
    /// Rotation of the icon in degrees clockwise.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-rotate")]
    public StoppedFloat IconRotate { get; set; } = new StoppedFloat { Default = 0.0f };

    /// <summary>
    /// In combination with symbol placement, the orientation of the icon;
    /// map, viewport, or auto.
    /// </summary>
    [JsonConverter(typeof(EnumConverter<MapAlignment>))]
    [JsonPropertyName("icon-rotation-alignment")]
    public MapAlignment IconRotationAlignment { get; set; } = MapAlignment.Auto;

    /// <summary>
    /// Scale factor of the icon.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("icon-size")]
    public StoppedFloat IconSize { get; set; } = new StoppedFloat { Default = 1.0f };

    /// <summary>
    /// How the icon is scaled to fit the text; "none", "width", "height", or "both".
    /// </summary>
    [JsonPropertyName("icon-text-fit")] public string IconTextFit { get; set; } = "none";

    /// <summary>
    /// Size of the padding around the text when the icon is fitted to it, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("icon-text-fit-padding")]
    public StoppedFloatArray IconTextFitPadding { get; set; } = new StoppedFloatArray { Default = [0, 0, 0, 0] };

    /// <summary>
    /// Whether symbols may be placed partially outside the tile bounds.
    /// </summary>
    [JsonPropertyName("symbol-avoid-edges")] public bool SymbolAvoidEdges { get; set; } = false;

    /// <summary>
    /// How symbols are placed along the geometry; point, line, or line-center.
    /// </summary>
    [JsonConverter(typeof(StoppedEnumConverter<SymbolPlacement>))]
    [JsonPropertyName("symbol-placement")]
    public StoppedEnum<SymbolPlacement> SymbolPlacement { get; set; } = new StoppedEnum<SymbolPlacement>
        { Default = Common.Enums.SymbolPlacement.Point };

    /// <summary>
    /// Sorts features in ascending order based on this value for symbol layers.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("symbol-sort-key")]
    public StoppedFloat SymbolSortKey { get; set; } = new StoppedFloat() { Default = 0f };

    /// <summary>
    /// Distance between symbols on a line, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("symbol-spacing")]
    public StoppedFloat SymbolSpacing { get; set; } = new StoppedFloat { Default = 250.0f };

    /// <summary>
    /// Whether symbols are elevated above 3D features (3D rendering only).
    /// </summary>
    [JsonPropertyName("symbol-z-elevate")] public bool SymbolZElevate { get; set; } = false;

    /// <summary>
    /// How symbols are ordered vertically; source, viewport-y, or auto.
    /// </summary>
    [JsonConverter(typeof(EnumConverter<SymbolZOrder>))]
    [JsonPropertyName("symbol-z-order")]
    public SymbolZOrder SymbolZOrder { get; set; } = SymbolZOrder.Auto;

    /// <summary>
    /// Whether the text may be placed overlapping other symbols.
    /// </summary>
    [JsonConverter(typeof(StoppedBooleanConverter))]
    [JsonPropertyName("text-allow-overlap")]
    public StoppedBoolean TextAllowOverlap { get; set; } = new StoppedBoolean() { Default = false };

    /// <summary>
    /// Part of the text placed closest to the anchor position.
    /// </summary>
    [JsonConverter(typeof(EnumConverter<Anchor>))]
    [JsonPropertyName("text-anchor")]
    public Anchor TextAnchor { get; set; } = Anchor.Center;

    /// <summary>
    /// Value used as the label text; supports field placeholders like "{name}".
    /// </summary>
    [JsonConverter(typeof(StoppedStringConverter))]
    [JsonPropertyName("text-field")]
    public StoppedString TextField { get; set; } = new StoppedString() { Default =  string.Empty };

    /// <summary>
    /// Font names used to display the text; empty means the style has no text-font.
    /// </summary>
    [JsonPropertyName("text-font")]
    [JsonConverter(typeof(TextFontConverter))]
    // Empty = the style does not specify a font; OMTTextBuilder falls back to
    // DefaultFontNames on its own, so that only really used fonts are reported
    public string[] TextFont { get; set; } = [];

    /// <summary>
    /// Whether other symbols may be placed overlapping the text, ignoring collision.
    /// </summary>
    [JsonConverter(typeof(StoppedBooleanConverter))]
    [JsonPropertyName("text-ignore-placement")]
    public StoppedBoolean TextIgnorePlacement { get; set; } = new StoppedBoolean() { Default = false };

    /// <summary>
    /// Text justification; left, center, or right.
    /// </summary>
    [JsonConverter(typeof(EnumConverter<TextJustify>))]
    [JsonPropertyName("text-justify")]
    public TextJustify TextJustify { get; set; } = TextJustify.Auto;

    /// <summary>
    /// Whether the text is kept upright when placed along a line.
    /// </summary>
    [JsonPropertyName("text-keep-upright")]
    public bool TextKeepUpright { get; set; } = true;

    /// <summary>
    /// Spacing between text characters, in ems.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-letter-spacing")]
    public StoppedFloat TextLetterSpacing { get; set; } = new StoppedFloat { Default = 0.0f };

    /// <summary>
    /// Line height for multi-line text, as a multiple of the text size.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-line-height")]
    public StoppedFloat TextLineHeight { get; set; } = new StoppedFloat { Default = 1.2f };

    /// <summary>
    /// Maximum angle change between consecutive characters for text on a line, in degrees.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-max-angle")]
    public StoppedFloat TextMaxAngle { get; set; } = new StoppedFloat { Default = 45.0f };

    /// <summary>
    /// Maximum width of a line of text, in ems, before wrapping.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-max-width")]
    public StoppedFloat TextMaxWidth { get; set; } = new StoppedFloat { Default = 10.0f };

    /// <summary>
    /// Offset of the text relative to its anchor, in ems.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatArrayConverter))]
    [JsonPropertyName("text-offset")]
    public StoppedFloatArray TextOffset { get; set; } = new StoppedFloatArray { Default = [0, 0] };

    /// <summary>
    /// Whether the text is optional (hidden when it overlaps other symbols
    /// while the icon is still shown).
    /// </summary>
    [JsonPropertyName("text-optional")]
    public bool TextOptional { get; set; } = false;

    /// <summary>
    /// Extra space around the text used for collision detection, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-padding")]
    public StoppedFloat TextPadding { get; set; } = new StoppedFloat { Default = 2.0f };

    /// <summary>
    /// Orientation of the text when the map is pitched; map, viewport, or auto.
    /// </summary>
    [JsonPropertyName("text-pitch-alignment")]
    public string TextPitchAlignment { get; set; } = "auto";

    /// <summary>
    /// Radial offset of the text, in the direction determined by the anchor, in ems.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-radial-offset")]
    public StoppedFloat TextRadialOffset { get; set; } = new StoppedFloat { Default = 0.0f };

    /// <summary>
    /// Rotation of the text in degrees clockwise.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-rotate")]
    public StoppedFloat TextRotate { get; set; } = new StoppedFloat { Default = 0.0f };

    /// <summary>
    /// In combination with symbol placement, the orientation of the text;
    /// map, viewport, or auto.
    /// </summary>
    [JsonConverter(typeof(EnumConverter<MapAlignment>))]
    [JsonPropertyName("text-rotation-alignment")]
    public MapAlignment TextRotationAlignment { get; set; }  = MapAlignment.Auto;

    /// <summary>
    /// Size of the text, in pixels.
    /// </summary>
    [JsonConverter(typeof(StoppedFloatConverter))]
    [JsonPropertyName("text-size")]
    public StoppedFloat TextSize { get; set; } = new StoppedFloat { Default = 16.0f };

    /// <summary>
    /// Capitalization of the text; none, uppercase, or lowercase.
    /// </summary>
    [JsonConverter(typeof(StoppedEnumConverter<TextTransform>))]
    [JsonPropertyName("text-transform")]
    public StoppedEnum<TextTransform> TextTransform { get; set; } = new StoppedEnum<TextTransform>() { Default = Common.Enums.TextTransform.None };

    /// <summary>
    /// Anchor positions the text may be placed at when it would collide otherwise;
    /// used instead of a fixed text-anchor.
    /// </summary>
    [JsonPropertyName("text-variable-anchor")]
    [JsonConverter(typeof(StringArrayConverter))]
    public string[] TextVariableAnchor { get; set; } = [];

    /// <summary>
    /// Preferred writing modes for text on a line, e.g. "horizontal" or "vertical".
    /// </summary>
    [JsonPropertyName("text-writing-mode")]
    public string[] TextWritingMode { get; set; } = [];

    /// <summary>
    /// Gets an empty layout instance with default property values.
    /// </summary>
    public static OMTLayerStyleLayout Empty = new OMTLayerStyleLayout();
}
