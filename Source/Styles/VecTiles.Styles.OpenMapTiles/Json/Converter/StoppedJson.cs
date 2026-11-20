using System.Text.Json.Nodes;
using VecTiles.Styles.OpenMapTiles.Expressions;

namespace VecTiles.Styles.OpenMapTiles.Json.Converter;

/// <summary>
/// Shared parsing of the "base" and "stops" members of stopped values
/// (Mapbox style spec property functions).
/// </summary>
internal static class StoppedJson
{
    /// <summary>
    /// Reads the optional "base" member. Missing values default to 1
    /// (linear interpolation like the style spec).
    /// </summary>
    public static float ReadBase(JsonObject node)
    {
        if (!node.TryGetPropertyValue("base", out var baseNode) || IsNull(baseNode))
            return 1f;

        return StoppedExpression.ToFloat(MatchExpression.ToValue(baseNode));
    }

    /// <summary>
    /// Enumerates the stop entries of the optional "stops" member. A stop is
    /// either an array [zoom, value] or an object {"Key": zoom, "Value": value}
    /// (which is the serialized form of a KeyValuePair). The value is handed to
    /// the caller unparsed, since every stopped type converts differently.
    /// </summary>
    public static IEnumerable<(float Zoom, JsonNode? Value)> Stops(JsonObject node)
    {
        if (!node.TryGetPropertyValue("stops", out var stopsNode) || stopsNode is not JsonArray stopArray)
            yield break;

        foreach (var stop in stopArray)
        {
            if (stop is JsonArray { Count: 2 } pair)
            {
                yield return (StoppedExpression.ToFloat(MatchExpression.ToValue(pair[0])), pair[1]);
            }
            else if (stop is JsonObject { Count: 2 } entry
                && entry.TryGetPropertyValue("Key", out var keyNode)
                && entry.TryGetPropertyValue("Value", out var valueNode))
            {
                yield return (StoppedExpression.ToFloat(MatchExpression.ToValue(keyNode)), valueNode);
            }
            else
            {
                yield return (0f, null);
            }
        }
    }

    private static bool IsNull(JsonNode? node)
        => node == null || node.GetValueKind() == System.Text.Json.JsonValueKind.Null;
}
