using Newtonsoft.Json;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox.Json;

/// <summary>
/// Class holding StoppedColor data in Json format
/// </summary>
public class JsonStoppedColor
{
    [JsonProperty("base")]
    public float Base { get; set; } = 1f;

    [JsonProperty("stops")]
    public IList<KeyValuePair<float, Color>> Stops { get; set; } = [];
}
