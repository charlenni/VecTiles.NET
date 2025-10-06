using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.Mapbox.Filter;
using VecTiles.Styles.Mapbox.Json.Converter;

namespace VecTiles.Styles.Mapbox;

public class MapboxLayerStyle : ILayerStyle
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
    public MapboxLayerStyleLayout Layout { get; set; } = new MapboxLayerStyleLayout();

    [JsonProperty("maxzoom")]
    public int MaxZoom { get; set; } = -1;

    [JsonProperty("metadata")]
    public JObject? Metadata { get; set; }

    [JsonProperty("minzoom")]
    public int MinZoom { get; set; } = -1;

    [JsonProperty("paint")]
    public MapboxLayerStylePaint Paint { get; set; } = new MapboxLayerStylePaint();

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
