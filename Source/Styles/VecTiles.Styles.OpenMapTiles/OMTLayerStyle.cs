using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles.Expressions;
using VecTiles.Styles.OpenMapTiles.Json.Converter;

namespace VecTiles.Styles.OpenMapTiles;

public class OMTLayerStyle : ILayerStyle
{
    [JsonPropertyName("id")]
    public string Name { get; set; } = string.Empty;

    [JsonConverter(typeof(EnumConverter<LayerStyleType>))]
    [JsonPropertyName("type")]
    public LayerStyleType StyleType { get; set; }

    [JsonConverter(typeof(ExpressionConverter))]
    [JsonPropertyName("filter")]
    public IExpression Filter { get; set; } = Expression.Empty;

    [JsonPropertyName("layout")]
    public OMTLayerStyleLayout Layout { get; set; } = new OMTLayerStyleLayout();

    [JsonPropertyName("maxzoom")]
    public float MaxZoom { get; set; } = -1;

    [JsonPropertyName("metadata")]
    public JsonObject? Metadata { get; set; }

    [JsonPropertyName("minzoom")]
    public float MinZoom { get; set; } = -1;

    [JsonPropertyName("paint")]
    public OMTLayerStylePaint Paint { get; set; } = new OMTLayerStylePaint();

    [JsonPropertyName("slot")]
    public string Slot { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("source-layer")]
    public string SourceLayer { get; set; } = string.Empty;

    [JsonPropertyName("interactive")]
    public bool Interactive { get; set; }

    public bool Visible => Layout?.Visibility == "visible";

    public override string ToString()
    {
        return Name + " " + StyleType;
    }

    public void Update(EvaluationContext context)
    {
        throw new NotImplementedException();
    }
}
