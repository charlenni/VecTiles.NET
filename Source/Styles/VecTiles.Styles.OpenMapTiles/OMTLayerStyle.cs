using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.OpenMapTiles.Filter;
using VecTiles.Styles.OpenMapTiles.Json.Converter;

namespace VecTiles.Styles.OpenMapTiles;

public class OMTLayerStyle : ILayerStyle
{
    [JsonProperty("id")]
    public string Name { get; set; } = string.Empty;

    [JsonConverter(typeof(EnumConverter<LayerStyleType>))]
    [JsonProperty("type")]
    public LayerStyleType StyleType { get; set; }

    [JsonConverter(typeof(FilterConverter))]
    [JsonProperty("filter")]
    public IFilter Filter { get; set; } = new EmptyFilter();

    [JsonProperty("layout")]
    public OMTLayerStyleLayout Layout { get; set; } = new OMTLayerStyleLayout();

    [JsonProperty("maxzoom")]
    public int MaxZoom { get; set; } = -1;

    [JsonProperty("metadata")]
    public JObject? Metadata { get; set; }

    [JsonProperty("minzoom")]
    public int MinZoom { get; set; } = -1;

    [JsonProperty("paint")]
    public OMTLayerStylePaint Paint { get; set; } = new OMTLayerStylePaint();

    [JsonProperty("slot")]
    public string Slot { get; set; } = string.Empty;

    [JsonProperty("source")]
    public string Source { get; set; } = string.Empty;

    [JsonProperty("source-layer")]
    public string SourceLayer { get; set; } = string.Empty;

    [JsonProperty("interactive")]
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
