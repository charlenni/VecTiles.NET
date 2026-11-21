using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles.Expressions;
using VecTiles.Styles.OpenMapTiles.Json.Converter;

namespace VecTiles.Styles.OpenMapTiles;

/// <summary>
/// A single layer of an OpenMapTiles (Mapbox-style) JSON style, containing the
/// filter, layout, and paint properties of the layer.
/// </summary>
public class OMTLayerStyle : ILayerStyle
{
    /// <inheritdoc />
    [JsonPropertyName("id")]
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    [JsonConverter(typeof(EnumConverter<LayerStyleType>))]
    [JsonPropertyName("type")]
    public LayerStyleType StyleType { get; set; }

    /// <inheritdoc />
    [JsonConverter(typeof(ExpressionConverter))]
    [JsonPropertyName("filter")]
    public IExpression Filter { get; set; } = Expression.Empty;

    /// <summary>
    /// Layout properties of the layer, such as visibility and symbol properties.
    /// </summary>
    [JsonPropertyName("layout")]
    public OMTLayerStyleLayout Layout { get; set; } = new OMTLayerStyleLayout();

    /// <inheritdoc />
    [JsonPropertyName("maxzoom")]
    public float MaxZoom { get; set; } = -1;

    /// <summary>
    /// Custom metadata of the layer, as present in the style file.
    /// </summary>
    [JsonPropertyName("metadata")]
    public JsonObject? Metadata { get; set; }

    /// <inheritdoc />
    [JsonPropertyName("minzoom")]
    public float MinZoom { get; set; } = -1;

    /// <summary>
    /// Paint properties of the layer, such as colors, opacity, and widths.
    /// </summary>
    [JsonPropertyName("paint")]
    public OMTLayerStylePaint Paint { get; set; } = new OMTLayerStylePaint();

    /// <summary>
    /// The slot the layer belongs to, used for style layer ordering.
    /// </summary>
    [JsonPropertyName("slot")]
    public string Slot { get; set; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("source-layer")]
    public string SourceLayer { get; set; } = string.Empty;

    /// <summary>
    /// Whether the layer is interactive.
    /// </summary>
    [JsonPropertyName("interactive")]
    public bool Interactive { get; set; }

    /// <inheritdoc />
    public bool Visible => Layout?.Visibility == "visible";

    /// <summary>
    /// Returns the layer name and type.
    /// </summary>
    /// <returns>The layer name followed by the layer type.</returns>
    public override string ToString()
    {
        return Name + " " + StyleType;
    }

    /// <inheritdoc />
    /// <exception cref="NotImplementedException">Not yet implemented for OpenMapTiles styles.</exception>
    public void Update(EvaluationContext context)
    {
        throw new NotImplementedException();
    }
}
