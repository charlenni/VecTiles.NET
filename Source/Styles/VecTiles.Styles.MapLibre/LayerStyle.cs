using NetTopologySuite.Features;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using VecTiles.Common.Enums;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.Styles.MapLibre.Expressions;
using VecTiles.Styles.MapLibre.Json.Converter;

namespace VecTiles.Styles.MapLibre;

/// <summary>
/// A single layer of a JSON style, containing the
/// filter, layout, and paint properties of the layer.
/// </summary>
public class LayerStyle : ILayerStyle, IFeatureSorting
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
    public LayerStyleLayout Layout { get; set; } = new LayerStyleLayout();

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
    public LayerStylePaint Paint { get; set; } = new LayerStylePaint();

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

    /// <inheritdoc />
    public IEnumerable<IFeature> SortFeatures(EvaluationContext context, IEnumerable<IFeature> features,
        FeatureSortKey sortKey)
    {
        var key = sortKey switch
        {
            FeatureSortKey.Fill => Layout.FillSortKey,
            FeatureSortKey.Line => Layout.LineSortKey,
            _ => Layout.CircleSortKey,
        };

        if (key.Expression == null)
        {
            return [.. features];
        }

        return [.. features
            .Select(f => (Feature: f, Key: key.Evaluate(context.WithFeature(f))))
            .OrderBy(t => t.Key)
            .Select(t => t.Feature)];
    }

    /// <summary>
    /// Returns the layer name and type.
    /// </summary>
    /// <returns>The layer name followed by the layer type.</returns>
    public override string ToString()
    {
        return Name + " " + StyleType;
    }

    /// <inheritdoc />
    /// <exception cref="NotImplementedException">Not yet implemented for MapLibre styles.</exception>
    public void Update(EvaluationContext context)
    {
        throw new NotImplementedException();
    }
}
